using OpenAI.Chat;

Console.Title = "OpenAI Chat Client 🤖";

ChatClient chatClient = new(
   model: "gpt-4o-mini",
   apiKey: Environment.GetEnvironmentVariable("OpenAI_API_Key"));

while (true)
{
    Console.WriteLine("Enter your message (or type 'exit' to quit):");

    var userPrompt = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userPrompt) || userPrompt.Equals("exit", StringComparison.CurrentCultureIgnoreCase))
        break;

    Console.WriteLine("Thinking...");

    await foreach (var message in chatClient.CompleteChatStreamingAsync(userPrompt))
    {
        foreach (var item in message.ContentUpdate)
        {
            Console.Write(item.Text);
        }
    }

    Console.WriteLine();
}

