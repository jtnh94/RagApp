# RagApp

A simple Retrieval-Augmented Generation (RAG) application built with ASP.NET Core, Qdrant vector database, and Google's Gemini AI. Upload documents (PDF or TXT) and ask questions about their content using AI-powered search and generation.

## Features

- **Document Ingestion**: Upload PDF or TXT files, automatically extract text, chunk it, and store embeddings in Qdrant
- **Intelligent Querying**: Ask questions about uploaded documents and get AI-generated answers based on relevant content
- **Vector Search**: Uses Qdrant for fast similarity search on document embeddings
- **Gemini Integration**: Leverages Google's Gemini models for text embeddings and answer generation
- **REST API**: Simple HTTP endpoints for integration
- **Web Interface**: React-based frontend for easy document upload and querying

## Prerequisites

- [.NET 10.0](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (v16 or later, for frontend)
- [Qdrant](https://qdrant.tech/) (local installation or cloud instance)
- Google Gemini API key (for embeddings and generation)

## Installation

1. **Clone the repository**:
   ```bash
   git clone <your-repo-url>
   cd RagApp
   ```

2. **Backend setup**:
   ```bash
   dotnet restore
   dotnet build
   ```

3. **Frontend setup** (optional):
   ```bash
   cd rag-ui
   npm install
   cd ..
   ```

## Configuration
Add your Qdrant endpoint and your ApiKeys to user-secrets
```
dotnet user-secrets set "Qdrant:Url" "YOUR_QDRANT_CLUSTER_URL"
dotnet user-secrets set "Qdrant:ApiKey" "YOUR_QDRANT_API_KEY"
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_KEY" 
```

### Qdrant Setup

- **Local Qdrant**: Install and run Qdrant locally, use `http://localhost:6334`
- **Qdrant Cloud**: Sign up at [Qdrant Cloud](https://cloud.qdrant.io), create a cluster, and use the provided URL and API key

### Gemini API

- Get your API key from [Google AI Studio](https://makersuite.google.com/app/apikey)
- The app uses `gemini-embedding-2` for embeddings (3072 dimensions) and `gemini-3.1-flash-lite-preview` for generation

### Frontend Configuration

If using the React frontend, update the API base URL in `rag-ui/src/config.js` (or equivalent) to point to your backend server (e.g., `http://localhost:5178`).

- Get your API key from [Google AI Studio](https://makersuite.google.com/app/apikey)
- The app uses `gemini-embedding-2` for embeddings (3072 dimensions) and `gemini-3.1-flash-lite-preview` for generation

## Getting Started

1. **Start the backend**:
   ```bash
   dotnet run
   ```
   The backend will run on `http://localhost:5178` (check `launchSettings.json` for the exact port).

2. **Start the frontend** (optional):
   ```bash
   cd rag-ui
   npm start
   ```
   The frontend will run on `http://localhost:3000`.

3. **Upload a document**:
   - **Via API**: Use PowerShell or curl to upload a PDF or TXT file:
     ```powershell
     $Form = @{ file = Get-Item "path/to/your/document.pdf" }
     Invoke-RestMethod -Uri "http://localhost:5178/ingest" -Method Post -Form $Form
     ```
   - **Via Frontend**: Open `http://localhost:3000`, use the upload interface.

4. **Ask questions**:
   - **Via API**: Send a POST request with a JSON body:
     ```powershell
     Invoke-RestMethod -Uri http://localhost:5178/query -Method Post -ContentType 'application/json' -Body '{"question": "How tall is the Eiffel Tower?"}'
     ```
   - **Via Frontend**: Use the query interface in the React app.

## API Endpoints

### POST /ingest
Upload a document for processing.

**Request**: Multipart form data with `file` field (PDF or TXT)
**Response**: JSON with success message and chunk count

**Example**:
```bash
curl -X POST -F "file=@document.pdf" http://localhost:5178/ingest
```

### POST /query
Ask a question about uploaded documents.

**Request**: JSON with `question` field
**Response**: JSON with `answer` field

**Example**:
```bash
curl -X POST -H "Content-Type: application/json" -d '{"question":"Summarize the document"}' http://localhost:5178/query
```

## Troubleshooting

- **Qdrant Connection Issues**: Ensure Qdrant is running and the URL/API key are correct
- **Gemini API Errors**: Verify your API key has sufficient quota and the correct permissions
- **Vector Dimension Errors**: The app recreates collections with correct dimensions (3072) automatically
- **File Upload Issues**: Ensure files are valid PDF/TXT and under size limits

## Architecture

- **Backend**: ASP.NET Core minimal API
- **Frontend**: React application (in `rag-ui` directory) for user interface
- **Vector Database**: Qdrant for storing and searching document embeddings
- **AI Models**: Google Gemini for text embeddings and answer generation
- **Text Processing**: PDF parsing with UglyToad.PdfPig, text chunking with configurable size/overlap

The frontend provides a web interface for document upload and querying, communicating with the backend API.

## License

This project is licensed under the MIT License - see the LICENSE file for details.