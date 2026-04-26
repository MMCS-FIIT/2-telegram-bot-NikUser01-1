using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TimeKillerBot;

public class TelegramBot
{
    private const string BotToken = "8627118897:AAE6BM0JBL8JMSGatfaWlXBlpMLx5pvYNH8";

    /// <summary>
    /// Инициализирует и обеспечивает работу бота до нажатия клавиши Esc
    /// </summary>
    public async Task Run()
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Инициализируем наш клиент, передавая ему токен
        var botClient = new TelegramBotClient(BotToken);

        // Служебные вещи для организации правильной работы с потоками
        using CancellationTokenSource cts = new CancellationTokenSource();

        // Разрешённые события, которые будет получать и обрабатывать наш бот
        ReceiverOptions receiverOptions = new ReceiverOptions()
        {
            AllowedUpdates = new[] { UpdateType.Message }
        };

        // Привязываем все обработчики и начинаем принимать сообщения для бота
        botClient.StartReceiving(
            updateHandler: OnMessageReceived,
            errorHandler: OnErrorOccured,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );

        var me = await botClient.GetMe(cancellationToken: cts.Token);


        Console.WriteLine($"Бот @{me.Username} запущен!");

        while (Console.ReadKey(true).Key != ConsoleKey.Escape) { }
        cts.Cancel();
    }

    /// <summary>
    /// Обработчик события получения сообщения
    /// </summary>
    async Task OnMessageReceived(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message is null)
        {
            return;
        }

        if (message.Text is not { } messageText)
        {
            return;
        }

        var chatId = message.Chat.Id;

        Console.WriteLine($"Получено сообщение в чате {chatId}: '{messageText}'");

        // Кнопки
        var keyboard = new ReplyKeyboardMarkup(new[] { new[] { new KeyboardButton("🔥 Топ рейтинга"), new KeyboardButton("🎲 Случайно") } })
        { ResizeKeyboard = true };

        switch (messageText)
        {
            case "/start":
                string welcome = $"Привет, {message.Chat.FirstName}! 👋\n" +
                                 "Я бот TimeKiller. Помогу убить время и выбрать крутой фильм на вечер.";
                await botClient.SendMessage(chatId, welcome, replyMarkup: keyboard, cancellationToken: cancellationToken);
                break;

            case "🔥 Топ рейтинга":
                await SendMovie(botClient, chatId, true, cancellationToken);
                break;

            case "🎲 Случайно":
                await SendMovie(botClient, chatId, false, cancellationToken);
                break;

            default:
                await botClient.SendMessage(chatId, "Я тебя не понимаю :(( " + "\n" + "Пользуйся кнопками в меню!", cancellationToken: cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Отправляет фильм пользователю
    /// </summary>
    static async Task SendMovie(ITelegramBotClient botClient, long chatId, bool onlyTop, CancellationToken cancellationToken)
    {
        try
        {
            // Проверяем наличие файла
            if (!File.Exists("10000 movies.csv"))
            {
                await botClient.SendMessage(chatId, "Ошибка: Файл не найден в папке с программой", cancellationToken: cancellationToken);
                return;
            }

            // Читаем строки
            var lines = File.ReadAllLines("10000 movies.csv", Encoding.UTF8).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

            if (lines.Count == 0)
            {
                await botClient.SendMessage(chatId, "Файл пуст.", cancellationToken: cancellationToken);
                return;
            }

            var rnd = new Random();
            // Разбиваем строки
            var movies = lines.Select(l => l.Split(',')).ToList();

            // Фильтрация по рейтингу через регулярку
            if (onlyTop)
            {
                // Используем регулярку, чтобы найти высокие рейтинги (от 8.5 до 10.0)
                Regex ratingRegex = new Regex(@"^(8\.[5-9]|9\.[0-9]|10(\.0)?)");

                movies = movies.Where(m =>
                    m.Length >= 4 && ratingRegex.IsMatch(m[3].Trim().Replace(",", "."))
                ).ToList();
            }

            if (movies.Count == 0)
            {
                await botClient.SendMessage(chatId, "Фильмы с рейтингом >= 8.5 не найдены.", cancellationToken: cancellationToken);
                return;
            }
            var selected = movies[rnd.Next(movies.Count)];

            string text = $"🎬 *{selected[2].Trim()}*\n⭐ Рейтинг: {selected[3].Trim()}\n📅 Дата выхода: {selected[1].Trim()}";

            await botClient.SendMessage(chatId, text, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await botClient.SendMessage(chatId, "Произошла ошибка при поиске фильма.", cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Обработчик исключений, возникших при работе бота
    /// </summary>
    Task OnErrorOccured(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",

            _ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }
}