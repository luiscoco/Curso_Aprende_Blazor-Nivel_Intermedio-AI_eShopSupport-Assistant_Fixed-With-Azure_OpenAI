# How to fix the Assistant in the eShopSupport App

## 1. 

## 2. 

## 3. 


1. We set the appsettings.json file with a connection to Azure OpenAI

```json
 "ConnectionStrings": {
   "chatcompletion": "Endpoint=https://myaiserviceluis.openai.azure.com/;Key=7815bb4b3b1f4243be82faba074236a9;Deployment=gpt-4o"
 }
```

2. We modify the middleware Program.cs, we can replace Ollama with Azure OpenAI

```csharp
// Use this if you want to use Ollama
//var chatCompletion = builder.AddOllama("chatcompletion").WithDataVolume();

var chatCompletion = builder.AddConnectionString("chatcompletion");
```

3. Please review the PreventStreamingWithFunctions.cs file

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

4. StaffBackendClient.cs->replace the function AssistantChatAsync with this code:

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






