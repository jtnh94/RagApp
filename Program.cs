using Qdrant.Client;
using Qdrant.Client.Grpc;
using UglyToad.PdfPig;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddHttpClient();
builder.Services.AddAntiforgery();

// Register Qdrant client as a singleton
builder.Services.AddSingleton<QdrantClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var qdrantUrl = config["Qdrant:Url"]!;
    var uri = new Uri(qdrantUrl);
    var host = uri.Host;
    return new QdrantClient(
        host: host,
        port: 6334,
        https: true,
        apiKey: config["Qdrant:ApiKey"]!
    );
});

var app = builder.Build();
app.UseCors("AllowReact");

const string CollectionName = "documents";
const int EmbeddingDim = 3072;

app.MapPost("/ingest", static async (
    IFormFile file,
    QdrantClient qdrant,
    IHttpClientFactory httpFactory,
    IConfiguration config
) => {
        // Extract text
        var text = string.Empty;
        var ext = Path.GetExtension(file.FileName).ToLower();
        using var stream = file.OpenReadStream();

        if(ext == ".pdf") {
            using var pdf = PdfDocument.Open(stream);
            var sb = new StringBuilder();
            foreach(var page in pdf.GetPages()) {
                sb.AppendLine(page.Text);
            }
            text = sb.ToString();
        } else if (ext == ".txt") {
            using var reader = new StreamReader(stream);
            text = await reader.ReadToEndAsync();
        } else {
            return Results.BadRequest(new { error = "Only .pdf and .txt files are supported." });
        }

        if(string.IsNullOrWhiteSpace(text)) {
            return Results.BadRequest(new { error = "Could not extract text from the file." });
        }

        // Chunk the text (~500 words per chunk, 50 word overlap)
        var chunks = ChunkText(text, chunkSize: 500, overlap: 50);

        // Ensure the Qdrant collection exists with correct dimensions
        var collections = await qdrant.ListCollectionsAsync();
        if (collections.Any(c => c == CollectionName))
        {
            await qdrant.DeleteCollectionAsync(CollectionName);
        }
        await qdrant.CreateCollectionAsync(CollectionName,
            new VectorParams
            {
                Size = EmbeddingDim,
                Distance = Distance.Cosine
            });

        // Embed each chunk and upsert into Qdrant
        var http = httpFactory.CreateClient();
        var apiKey = config["Gemini:ApiKey"]!;
        var points = new List<PointStruct>();
        for(int i = 0; i < chunks.Count; i++){
            var embedding = await GetEmbedding(http, apiKey, chunks[i]);
            var vector = new Vector();
            vector.Data.AddRange(embedding);
            var point = new PointStruct{
                Id = new PointId { Uuid = Guid.NewGuid().ToString() },
                Vectors = new Vectors { Vector = vector }
            };
            point.Payload["text"] = chunks[i];
            point.Payload["chunk_index"] = i.ToString();
            points.Add(point);
        }
        await qdrant.UpsertAsync(CollectionName, points);
        return Results.Ok(new { message = $"Ingested {chunks.Count} chunks from {file.FileName}." });
    }
).DisableAntiforgery();

app.MapPost("/query", async (
    HttpRequest request,
    QdrantClient qdrant,
    IHttpClientFactory httpFactory,
    IConfiguration config
) => {
    var body = await request.ReadFromJsonAsync<QueryRequest>();
    if(body?.Question is null) return Results.BadRequest(new { error = "question is required." });
    var http = httpFactory.CreateClient();
    var apiKey = config["Gemini:ApiKey"]!;

    // Embed the question
    var questionVector = await GetEmbedding(http, apiKey, body.Question);

    // Retrieve the top 5 most relevant chunks
    var results = await qdrant.SearchAsync(
        CollectionName, questionVector, limit: 5, payloadSelector: new WithPayloadSelector { Enable = true }
    );

    if(!results.Any()) return Results.Ok(new { answer = "No relevant content found. Please ingest a document first." });

    // Build context from retrieved chunks
    var context = string.Join("\n\n---\n\n",
    results.Select(r => r.Payload["text"].StringValue));

    // Send context + question to Gemini
    var prompt = $"""
    You are a helpful assistant answering questions about a document.
    Use only the context provided below to answer the question.
    If the answer is not in the context, say so clearly.
    Do not use Markdown formatting. Use plain text only.

    Context:
    {context}

    Question: {body.Question}
    """;

    var geminiUrl = $"https://generativeLanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite-preview:generateContent?key={apiKey}";
    var geminiBody = new {contents = new[] { new { parts = new[] { new { text = prompt } } } } };
    var geminiResponse = await http.PostAsJsonAsync(geminiUrl, geminiBody);

    if(!geminiResponse.IsSuccessStatusCode) {
        var err = await geminiResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Gemini error {geminiResponse.StatusCode}: {err}");
        return Results.StatusCode(502);
    }

    var geminiResult = await geminiResponse.Content.ReadFromJsonAsync<GeminiResponse>();
    var answer = geminiResult?.Candidates?[0]?.Content?.Parts?[0]?.Text ?? "No answer generated.";
    return Results.Ok(new { answer });
});

app.Run();

// Text chunking helper
static List<string> ChunkText(string text, int chunkSize, int overlap){
    var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var chunks = new List<string>();
    int i = 0;
    while(i < words.Length){
        chunks.Add(string.Join(' ', words.Skip(i).Take(chunkSize)));
        i += chunkSize - overlap;
    }
    return chunks;
}

// Gemini embedding helper
static async Task<float[]> GetEmbedding(HttpClient http, string apiKey, string text){
    var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-2:embedContent?key={apiKey}";
    var body = new {
        content = new { parts = new[] { new { text }}} 
    };
    var response = await http.PostAsJsonAsync(url, body);
    response.EnsureSuccessStatusCode();
    var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
    return result!.Embedding.Values.ToArray();
}

// Models
record QueryRequest(string Question);
record EmbeddingResponse(EmbeddingValues Embedding);
record EmbeddingValues(float[] Values);
record GeminiResponse(GeminiCandidate[]? Candidates);
record GeminiCandidate(GeminiContent? Content);
record GeminiContent(GeminiPart[]? Parts);
record GeminiPart(string? Text);
