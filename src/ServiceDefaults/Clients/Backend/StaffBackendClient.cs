using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Web;

namespace eShopSupport.ServiceDefaults.Clients.Backend;

public class StaffBackendClient(HttpClient http)
{
    public async Task<ListTicketsResult> ListTicketsAsync(ListTicketsRequest request)
    {
        var result = await http.PostAsJsonAsync("/tickets", request);
        return (await result.Content.ReadFromJsonAsync<ListTicketsResult>())!;
    }

    public Task<TicketDetailsResult> GetTicketDetailsAsync(int ticketId)
        => http.GetFromJsonAsync<TicketDetailsResult>($"/tickets/{ticketId}")!;

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



    public async Task<Stream?> ReadManualAsync(string file, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/manual?file={HttpUtility.UrlEncode(file)}");
        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsStreamAsync(cancellationToken) : null;
    }

    public async Task SendTicketMessageAsync(int ticketId, SendTicketMessageRequest message)
    {
        await http.PostAsJsonAsync($"/api/ticket/{ticketId}/message", message);
    }

    public async Task UpdateTicketDetailsAsync(int ticketId, int? productId, TicketType ticketType, TicketStatus ticketStatus)
    {
        await http.PutAsJsonAsync($"/api/ticket/{ticketId}", new UpdateTicketDetailsRequest(productId, ticketType, ticketStatus));
    }

    public Task<FindCategoriesResult[]> FindCategoriesAsync(string searchText)
    {
        return http.GetFromJsonAsync<FindCategoriesResult[]>($"/api/categories?searchText={HttpUtility.UrlEncode(searchText)}")!;
    }

    public Task<FindCategoriesResult[]> FindCategoriesAsync(IEnumerable<int> categoryIds)
    {
        return http.GetFromJsonAsync<FindCategoriesResult[]>($"/api/categories?ids={string.Join(",", categoryIds)}")!;
    }

    public Task<FindProductsResult[]> FindProductsAsync(string searchText)
    {
        return http.GetFromJsonAsync<FindProductsResult[]>($"/api/products?searchText={HttpUtility.UrlEncode(searchText)}")!;
    }
}

public record ListTicketsRequest(TicketStatus? FilterByStatus, List<int>? FilterByCategoryIds, int? FilterByCustomerId, int StartIndex, int MaxResults, string? SortBy, bool? SortAscending);

public record ListTicketsResult(ICollection<ListTicketsResultItem> Items, int TotalCount, int TotalOpenCount, int TotalClosedCount);

public record ListTicketsResultItem(
    int TicketId, TicketType TicketType, TicketStatus TicketStatus, DateTime CreatedAt, string CustomerFullName, string? ProductName, string? ShortSummary, int? CustomerSatisfaction, int NumMessages);

public record TicketDetailsResult(
    int TicketId, DateTime CreatedAt, int CustomerId, string CustomerFullName, string? ShortSummary, string? LongSummary,
    int? ProductId, string? ProductBrand, string? ProductModel,
    TicketType TicketType, TicketStatus TicketStatus,
    int? CustomerSatisfaction, ICollection<TicketDetailsResultMessage> Messages);

public record TicketDetailsResultMessage(int MessageId, DateTime CreatedAt, bool IsCustomerMessage, string MessageText);

public record UpdateTicketDetailsRequest(int? ProductId, TicketType TicketType, TicketStatus TicketStatus);

public record AssistantChatRequest(
    int? ProductId,
    string? CustomerName,
    string? TicketSummary,
    string? TicketLastCustomerMessage,
    IReadOnlyList<AssistantChatRequestMessage> Messages);

public class AssistantChatRequestMessage
{
    public bool IsAssistant { get; set; }
    public required string Text { get; set; }
}

public record AssistantChatReplyItem(AssistantChatReplyItemType Type, string Text, int? SearchResultId = null, int? SearchResultProductId = null, int? SearchResultPageNumber = null);

public enum AssistantChatReplyItemType { AnswerChunk, Search, SearchResult, IsAddressedToCustomer };

public record SendTicketMessageRequest(string Text);

public record FindCategoriesResult(int CategoryId)
{
    public required string Name { get; set; }
}

public record FindProductsResult(int ProductId, string Brand, string Model);

public enum TicketStatus
{
    Open,
    Closed,
}

public enum TicketType
{
    Question,
    Idea,
    Complaint,
    Returns,
}

public record CreateTicketRequest(
    string? ProductName,
    string Message);
