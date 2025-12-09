using HtmlAgilityPack;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace HttpNewsPAT
{
    public class Program
    {
        static string logFilePath = "debug_trace.log";
        static HttpClient httpClient = new HttpClient();

        static string connectionPath = "Server=127.0.0.1;port=3306;Database=news;Uid=root;Pwd=;";

        static async Task Main(string[] args)
        {
            // Логика выбора даты
            string targetDate = DateTime.Now.ToString("dd.MM.yyyy"); // Получаем текущую дату (например, 09.12.2025)
            bool readyToParse = true;

            Console.Write($"Использовать сегодняшнюю дату ({targetDate})? (да/нет): ");
            string useToday = Console.ReadLine()?.ToLower();

            if (useToday == "нет" || useToday == "no" || useToday == "n")
            {
                Console.Write("Выбрать другую дату? (да/нет): ");
                string chooseOther = Console.ReadLine()?.ToLower();

                if (chooseOther == "да" || chooseOther == "yes" || chooseOther == "y")
                {
                    Console.Write("Введите дату в формате dd.mm.yyyy: ");
                    targetDate = Console.ReadLine();

                    // Простая проверка что строка не пустая
                    if (string.IsNullOrWhiteSpace(targetDate))
                    {
                        Console.WriteLine("Дата не введена.");
                        readyToParse = false;
                    }
                }
                else
                {
                    // если нет то ничего не делаем
                    Console.WriteLine("Дата не выбрана. Работа программы завершена.");
                    readyToParse = false;
                }
            }

            // Если дата выбрана продолжаем выполнение
            if (readyToParse)
            {
                // Подставляем дату в URL
                string siteUrl = $"https://ppk59.ru/stantions/permI/?data={targetDate}";

                Console.WriteLine($"\nБудет выполнен парсинг по URL: {siteUrl}");

                // Блок авторизации 
                Console.Write("Требуется авторизация? (да/нет): ");
                string needAuth = Console.ReadLine()?.ToLower();
                if (needAuth == "да")
                {
                    // Логика авторизации (заглушка)
                    Console.WriteLine("Пропуск авторизации...");
                }

                // Получение контента
                string pageContent = await GetContentAsync(siteUrl);

                if (!string.IsNullOrEmpty(pageContent))
                {
                    // Парсим расписание
                    List<NewsData> scheduleList = ParseScheduleFromHtml(pageContent, siteUrl);

                    Console.WriteLine($"Найдено поездов: {scheduleList.Count}");

                    if (scheduleList.Count > 0)
                    {
                        Console.Write("\nДобавить расписание в базу данных? (да/нет): ");
                        string addToDb = Console.ReadLine()?.ToLower();

                        if (addToDb == "да" || addToDb == "yes" || addToDb == "y" || addToDb == "д")
                        {
                            await AddScheduleToDatabaseAsync(scheduleList);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Список пуст. Возможно, формат даты неверный или поездов нет.");
                    }
                }
            }

            WriteToLog("Завершение");

            Console.WriteLine($"\nЛог сохранен: {logFilePath}");
            Console.WriteLine("\nНажмите любую клавишу...");
            Console.ReadKey();
        }


        public static async Task<string> GetContentAsync(string url)
        {
            WriteToLog($"Запрос страницы: {url}");
            try
            {
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                var response = await httpClient.GetAsync(url);
                WriteToLog($"Статус: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    Console.WriteLine($"Ошибка: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                return null;
            }
        }

        public static List<NewsData> ParseScheduleFromHtml(string htmlCode, string url)
        {
            var scheduleList = new List<NewsData>();
            var html = new HtmlDocument();
            html.LoadHtml(htmlCode);
            var document = html.DocumentNode;

            var tables = document.SelectNodes("//table[contains(@class, 'rasp')]");

            if (tables == null)
            {
                Console.WriteLine("Таблицы с расписанием не найдены.");
                return scheduleList;
            }

            foreach (var table in tables)
            {
                var rows = table.SelectNodes(".//tr[td]");
                if (rows == null) continue;

                foreach (var row in rows)
                {
                    var cells = row.SelectNodes("td");
                    if (cells != null && cells.Count >= 5)
                    {
                        var item = new NewsData();
                        string trainNum = cells[0].InnerText.Trim();
                        string route = cells[1].InnerText.Trim();
                        route = System.Text.RegularExpressions.Regex.Replace(route, @"\s+", " ");
                        item.Name = $"Поезд {trainNum}: {route}";

                        var arrivalNode = cells[2].SelectSingleNode(".//div[contains(@class, 'perm-time')]");
                        string arrival = arrivalNode != null ? arrivalNode.InnerText.Trim() : cells[2].InnerText.Trim();

                        var departureNode = cells[3].SelectSingleNode(".//div[contains(@class, 'perm-time')]");
                        string departure = departureNode != null ? departureNode.InnerText.Trim() : cells[3].InnerText.Trim();

                        string days = cells[4].InnerText.Trim();

                        item.Description = $"Прибытие: {arrival} | Отправление: {departure} | Дни: {days}";
                        item.ImageUrl = "https://ppk59.ru/images/favicon.ico";

                        scheduleList.Add(item);
                    }
                }
            }
            WriteToLog($"Распарсено элементов расписания: {scheduleList.Count}");
            return scheduleList;
        }

        public static async Task AddScheduleToDatabaseAsync(List<NewsData> newsList)
        {
            if (newsList.Count == 0) return;

            try
            {
                using (var connection = new MySqlConnection(connectionPath))
                {
                    await connection.OpenAsync();

                    int currentId = 0;
                    string maxIdQuery = "SELECT MAX(id) FROM news";
                    using (var maxIdCommand = new MySqlCommand(maxIdQuery, connection))
                    {
                        var result = await maxIdCommand.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            currentId = Convert.ToInt32(result);
                        }
                    }

                    int addedCount = 0;
                    foreach (var news in newsList)
                    {
                        currentId++;
                        string insertQuery = @"INSERT INTO news (id, img, name, description) VALUES (@id, @img, @name, @description)";

                        using (var command = new MySqlCommand(insertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@id", currentId);
                            command.Parameters.AddWithValue("@img", news.ImageUrl);
                            command.Parameters.AddWithValue("@name", news.Name);
                            command.Parameters.AddWithValue("@description", news.Description);

                            await command.ExecuteNonQueryAsync();
                            addedCount++;
                        }
                    }
                    Console.WriteLine($"Успешно добавлено записей: {addedCount}");
                    WriteToLog($"Успешно добавлено записей: {addedCount}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при записи в БД: {ex.Message}");
                WriteToLog($"Ошибка при записи в БД: {ex.Message}");
            }
        }

        public static void WriteToLog(string message)
        {
            try
            {
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка записи в лог: {ex.Message}");
            }
        }

        public class NewsData
        {
            public string ImageUrl { get; set; } = "";
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
        }
    }
}