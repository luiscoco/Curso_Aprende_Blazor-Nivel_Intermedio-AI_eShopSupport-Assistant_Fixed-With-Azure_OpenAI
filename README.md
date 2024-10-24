# How to fix the Assistant in the eShopSupport App

The original **eshopsupport** github repo is in this URL: https://github.com/dotnet/eShopSupport

## 1. We create a new Azure OpenAI service with a gpt-4o deployment

![image](https://github.com/user-attachments/assets/e72c1622-f604-4473-8867-aca0110b7a5a)

![image](https://github.com/user-attachments/assets/a6c8027f-1648-4b29-897c-643fc83f9bb0)

![image](https://github.com/user-attachments/assets/ee886b37-a3f6-466d-84de-c94587930946)

![image](https://github.com/user-attachments/assets/5f5cdcb3-c08f-48c2-9f43-621b7792576b)

![image](https://github.com/user-attachments/assets/ee56230e-e092-4d46-a0eb-afbf2bdf21ac)

![image](https://github.com/user-attachments/assets/a3285ebd-13f9-423b-b143-e4188fa7d027)

## 2. We modify the **appsettings.json** file with a connection to Azure OpenAI

![image](https://github.com/user-attachments/assets/b95de13a-9650-49f2-956c-1d3491f43b77)

```json
 "ConnectionStrings": {
   "chatcompletion": "Endpoint=https://myaiserviceluis.openai.azure.com/;Key=7815bb4b3b1f4243be82faba074236a9;Deployment=gpt-4o"
 }
```

## 3. We modify the middleware Program.cs, we can replace Ollama with Azure OpenAI

![image](https://github.com/user-attachments/assets/f338616e-71d6-45d4-9f50-67105e228e82)

```csharp
// Use this if you want to use Ollama
//var chatCompletion = builder.AddOllama("chatcompletion").WithDataVolume();

var chatCompletion = builder.AddConnectionString("chatcompletion");
```

## 4. Please review the PreventStreamingWithFunctions.cs file

```csharp
 private class PreventStreamingWithFunctions(IChatClient innerClient) : DelegatingChatClient(innerClient)
 {
     public override Task<ChatCompletion> CompleteAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
     {
         // Temporary workaround for an issue in CompleteAsync<T>. Although OpenAI models are happy to
         // receive system messages at the end of the conversation, it causes a lot of problems for
         // Llama 3. So replace the schema prompt role with User. We'll update CompleteAsync<T> to
         // do this natively in the next update.

         if (chatMessages.Count > 1)
         {
             var lastMessage = chatMessages[^1]; // Get the last message directly using index
             if (lastMessage.Role == ChatRole.System && lastMessage.Text?.Contains("$schema") == true)
             {
                 lastMessage.Role = ChatRole.User; // Change the role directly in the chatMessages list
             }
         }

         return base.CompleteAsync(chatMessages, options, cancellationToken);
     }
```

## 5. StaffBackendClient.cs->replace the function AssistantChatAsync with this code:

```csharp
public async IAsyncEnumerable<AssistantChatReplyItem> AssistantChatAsync(AssistantChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
{
    var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/assistant/chat")
    {
        Content = JsonContent.Create(request),
    };

    // Send the HTTP request
    var response = await http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

    // Ensure the response is successful
    if (!response.IsSuccessStatusCode)
    {
        // Log the error and throw an exception or handle it appropriately
        Console.WriteLine($"Error: Received HTTP {(int)response.StatusCode} - {response.ReasonPhrase}");
        throw new HttpRequestException($"Unexpected HTTP status code: {(int)response.StatusCode}");
    }

    // Read the response stream
    var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

    // Call a helper function to deserialize and return the items one by one
    await foreach (var item in DeserializeAndYieldItemsAsync(stream, cancellationToken))
    {
        yield return item;
    }
}

private async IAsyncEnumerable<AssistantChatReplyItem> DeserializeAndYieldItemsAsync(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken)
{
    // Variable to store the deserialized items
    IAsyncEnumerable<AssistantChatReplyItem>? items;

    // Try to deserialize outside of the yield loop
    try
    {
        items = JsonSerializer.DeserializeAsyncEnumerable<AssistantChatReplyItem>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken: cancellationToken);
    }
    catch (JsonException jsonEx)
    {
        // Log the error for debugging purposes
        Console.WriteLine($"JSON Deserialization error: {jsonEx.Message}");
        throw; // Optionally re-throw or handle the exception
    }
    catch (Exception ex)
    {
        // Handle any other errors
        Console.WriteLine($"Error during chat processing: {ex.Message}");
        throw;
    }

    // Yield each item, now outside of the try block
    await foreach (var item in items)
    {
        if (item is not null)
        {
            yield return item;
        }
    }
}
```






