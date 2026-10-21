using Microsoft.Playwright;
using System.Text.RegularExpressions;

const string url = "https://vca.eveselibaspunkts.lv/lv/Booking/AvailableTimeSlots?EmbeddedCode=010064120&InstitutionCode=2&SpecialistCode=11097910418&SpecialityCode=P13&ServiceCode";
const string doctor = "Pasters Vitālijs";
const string fullText = "visi atvērtie pieraksta laiki ir aizpildīti";
const string stateFile = "state.txt";

var token = Environment.GetEnvironmentVariable("TG_TOKEN")
    ?? throw new Exception("Не задан секрет TG_TOKEN");
var chatId = Environment.GetEnvironmentVariable("TG_CHAT_ID")
    ?? throw new Exception("Не задан секрет TG_CHAT_ID");

using var pw = await Playwright.CreateAsync();
await using var browser = await pw.Chromium.LaunchAsync();
var page = await browser.NewPageAsync();

await page.GotoAsync(url);

// ждём, пока на странице появится имя врача (значит, всё загрузилось)
await page.GetByText(doctor).First.WaitForAsync(new() { Timeout = 30000 });

// закрываем окно cookies, если оно есть
var cookie = page.GetByText("Atsakos");
if (await cookie.CountAsync() > 0)
    await cookie.First.ClickAsync();

await Task.Delay(2000); // даём слотам подгрузиться

var body = await page.InnerTextAsync("body");
var i = body.IndexOf(doctor, StringComparison.OrdinalIgnoreCase);
var block = body.Substring(i, Math.Min(600, body.Length - i));
block = Regex.Replace(block, @"\s+", " "); // убираем переносы и неразрывные пробелы

bool isFull = block.Contains(fullText, StringComparison.OrdinalIgnoreCase);
var prev = File.Exists(stateFile) ? File.ReadAllText(stateFile).Trim() : "full";

// уведомляем только когда состояние сменилось с "занято" на "свободно"
if (!isFull && prev != "free")
    await SendTelegram(token, chatId, $"Появилась запись к {doctor}! {url}");

File.WriteAllText(stateFile, isFull ? "full" : "free");
Console.WriteLine(isFull ? "Занято" : "Есть свободное время");

static async Task SendTelegram(string token, string chatId, string msg)
{
    using var http = new HttpClient();
    var resp = await http.GetAsync(
        $"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(msg)}");
    resp.EnsureSuccessStatusCode();
}
