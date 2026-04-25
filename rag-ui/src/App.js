import  { useState } from 'react';

const API = 'http://localhost:5178';

function App() {
  const [file, setFile] = useState(null);
  const [ingestMessage, setIngestMessage] = useState('');
  const [ingesting, setIngesting] = useState(false);
  const [question, setQuestion] = useState('');
  const [answer, setAnswer] = useState('');
  const [querying, setQuerying] = useState(false);
  const [error, setError] = useState('');

  const handleIngest = async () => {
    if (!file) return;
    setIngesting(true);
    setIngestMessage('');
    setError('');

    const form = new FormData();
    form.append('file', file);

    try {
      const res = await fetch(`${API}/ingest`, { method: 'POST', body: form });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || 'Ingest failed');
      setIngestMessage(data.message);
    } catch (err) {
      setError(err.message);
    } finally {
      setIngesting(false);
    };
  }

  const handleQuery = async () => {
    if (!question) return;
    setQuerying(true);
    setAnswer('');
    setError('');

    try {
      const res = await fetch(`${API}/query`, { 
        method: 'POST', 
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ question })
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || 'Query failed');
      setAnswer(data.answer);
    } catch (err) {
      setError(err.message);
    } finally {
      setQuerying(false);
    }
  };

  return (
    <div style={{ maxWidth: 720, margin: '60px auto', fontFamily: 'sans-serif', padding: '0 20px'}}>
      <h1>Document Q&A</h1>
      <p>Upload a PDF or text file, then ask questions about it.</p>

      <div style={{ background: '#f8f8f8', borderRadius: 8, padding: 20, marginBottom: 24 }}>
        <h2 style={{ marginTop: 0 }}>1. Upload Document</h2>
        <input type="file" accept=".pdf,.txt" onChange={e => setFile(e.target.files[0])} />
        <br /> <br />
        <button onClick={handleIngest} disabled={!file || ingesting}>
          {ingesting ? 'Processing...' : 'Upload & Process'}
        </button>
        {ingestMessage && <p style={{ color: '#2e7d32', marginTop: 8 }}>{ingestMessage}</p>}
      </div>

      <div style={{ background: '#f8f8f8', borderRadius: 8, padding: 20 }}>
        <h2 style={{ marginTop: 0 }}>2. Ask a Question</h2>
        <input
          type="text"
          value={question}
          onChange={e => setQuestion(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && handleQuery()}
          placeholder="What does the document say about...?"
          style={{ width: '100%', padding: 10, fontSize: 15, boxSizing: 'border-box', marginBottom: 10 }}
        />
        <button onClick={handleQuery} disabled={!question || querying}>
          {querying ? 'Processing...' : 'Ask Question'}
        </button>
        {answer && (
          <div style={{ marginTop: 16, background: '#fff', border: '1px solid #ddd', borderRadius: 6, padding: 20, whiteSpace: 'pre-wrap', lineHeight: 1.7 }}>
            {answer}
          </div>
        )}
        {error && <p style={{ color: '#c62828', marginTop: 16 }}>{error}</p>}
      </div>
    </div>
  );
};

export default App;
