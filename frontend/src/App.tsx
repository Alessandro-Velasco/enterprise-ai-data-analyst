import { useState, useEffect } from 'react';
import axios from 'axios';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, LineChart, Line } from 'recharts';

const API_URL = 'http://localhost:5000/api/DataAnalysis';

interface Dataset {
  id: string;
  name: string;
  fileType: string;
}

interface QueryResult {
  generatedSQL: string;
  resultJSON: string;
  visualizationType: string;
}

function App() {
  const [datasets, setDatasets] = useState<Dataset[]>([]);
  const [selectedDataset, setSelectedDataset] = useState<string>('');
  const [query, setQuery] = useState('');
  const [result, setResult] = useState<QueryResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [uploading, setUploading] = useState(false);

  useEffect(() => {
    loadDatasets();
  }, []);

  const loadDatasets = async () => {
    try {
      const res = await axios.get(`${API_URL}/datasets`);
      setDatasets(res.data);
    } catch (err) {
      console.error("Errore nel caricamento dei dataset", err);
    }
  };

  const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploading(true);
    const formData = new FormData();
    formData.append('file', file);

    try {
      await axios.post(`${API_URL}/upload`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      alert('✅ Dataset caricato con successo!');
      loadDatasets();
      e.target.value = '';
    } catch (err) {
      alert('❌ Errore nel caricamento del file.');
    } finally {
      setUploading(false);
    }
  };

  const executeQuery = async () => {
    if (!selectedDataset || !query) {
      alert('Seleziona un dataset e scrivi una domanda!');
      return;
    }
    
    setLoading(true);
    setResult(null);
    try {
      const res = await axios.post(
        `${API_URL}/query?datasetId=${selectedDataset}`,
        `"${query}"`,
        { headers: { 'Content-Type': 'application/json' } }
      );
      setResult(res.data);
    } catch (err) {
      alert('❌ Errore nell\'esecuzione della query AI.');
    } finally {
      setLoading(false);
    }
  };

  const getParsedData = () => {
    if (!result?.resultJSON) return [];
    try {
      return JSON.parse(result.resultJSON);
    } catch {
      return [];
    }
  };

  const chartData = getParsedData().map((row: any) => {
    const keys = Object.keys(row);
    return { 
      name: String(row[keys[0]]), 
      value: parseFloat(row[keys[1]]) || 0 
    };
  });

  return (
    <div className="min-h-screen bg-slate-900 text-slate-50 p-6">
      <div className="max-w-7xl mx-auto">
        {/* Header */}
        <header className="mb-8 border-b border-slate-700 pb-4">
          <h1 className="text-3xl font-bold text-blue-400 flex items-center gap-3">
            🗄️ Enterprise AI Data Analyst
          </h1>
          <p className="text-slate-400 mt-2">Analizza i tuoi dati CSV con l'intelligenza artificiale locale (Ollama)</p>
        </header>

        {/* 3 Cards Grid */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-8">
          
          {/* Upload Card */}
          <div className="bg-slate-800 p-6 rounded-xl border border-slate-700 shadow-lg">
            <h2 className="text-xl font-bold mb-4 text-blue-400">📤 1. Carica Dataset</h2>
            <label className="block w-full text-sm text-slate-400 file:mr-4 file:py-2.5 file:px-4 file:rounded-lg file:border-0 file:bg-blue-600 file:text-white hover:file:bg-blue-700 cursor-pointer transition">
              {uploading ? 'Caricamento...' : 'Scegli file CSV'}
              <input 
                type="file" 
                accept=".csv" 
                onChange={handleUpload} 
                className="hidden" 
                disabled={uploading} 
              />
            </label>
          </div>

          {/* Select Card */}
          <div className="bg-slate-800 p-6 rounded-xl border border-slate-700 shadow-lg">
            <h2 className="text-xl font-bold mb-4 text-green-400">🗃️ 2. Seleziona Dati</h2>
            <select
              value={selectedDataset}
              onChange={(e) => setSelectedDataset(e.target.value)}
              className="w-full bg-slate-900 border border-slate-600 text-white p-3 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none"
            >
              <option value="">-- Seleziona un dataset --</option>
              {datasets.map(d => (
                <option key={d.id} value={d.id}>{d.name} ({d.fileType})</option>
              ))}
            </select>
          </div>

          {/* Query Card */}
          <div className="bg-slate-800 p-6 rounded-xl border border-slate-700 shadow-lg">
            <h2 className="text-xl font-bold mb-4 text-purple-400">💬 3. Fai una Domanda</h2>
            <textarea
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Es: Mostrami il totale delle quantità per città"
              className="w-full bg-slate-900 border border-slate-600 text-white p-3 rounded-lg h-24 resize-none focus:ring-2 focus:ring-purple-500 outline-none mb-3"
            />
            <button
              onClick={executeQuery}
              disabled={loading || !selectedDataset}
              className="w-full bg-purple-600 hover:bg-purple-700 disabled:bg-slate-600 disabled:cursor-not-allowed text-white p-3 rounded-lg font-bold flex items-center justify-center gap-2 transition"
            >
              {loading ? '⏳ Analisi in corso...' : '🚀 Esegui Query AI'}
            </button>
          </div>
        </div>

        {/* Results Section */}
        {result && (
          <div className="bg-slate-800 p-6 rounded-xl border border-slate-700 shadow-lg">
            <h2 className="text-xl font-bold mb-4 text-green-400">💻 Risultato Analisi</h2>
            
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
              {/* SQL Generated */}
              <div>
                <p className="text-slate-400 text-sm mb-2">Query SQL Generata:</p>
                <code className="block bg-slate-900 border border-slate-700 p-4 rounded-lg text-green-400 font-mono text-sm overflow-x-auto">
                  {result.generatedSQL}
                </code>
              </div>

              {/* Visualization Type */}
              <div>
                <p className="text-slate-400 text-sm mb-2">
                  Visualizzazione consigliata: <span className="text-blue-400 font-bold uppercase">{result.visualizationType}</span>
                </p>
              </div>
            </div>

            {/* Tabella dei risultati */}
            <div className="mt-6">
              <p className="text-slate-400 text-sm mb-3">📊 Dati Risultanti:</p>
              <div className="bg-slate-900 border border-slate-700 rounded-lg overflow-x-auto">
                {(() => {
                  try {
                    const data = JSON.parse(result.resultJSON);
                    if (data.error) {
                      return <div className="p-4 text-red-400">❌ Errore: {data.error}</div>;
                    }
                    if (!Array.isArray(data) || data.length === 0) {
                      return <div className="p-4 text-slate-500">Nessun risultato trovato</div>;
                    }
                    const headers = Object.keys(data[0]);
                    return (
                      <table className="w-full text-sm">
                        <thead className="bg-slate-800 border-b border-slate-700">
                          <tr>
                            {headers.map(h => (
                              <th key={h} className="px-4 py-3 text-left text-slate-300 font-semibold">{h}</th>
                            ))}
                          </tr>
                        </thead>
                        <tbody>
                          {data.map((row: any, idx: number) => (
                            <tr key={idx} className="border-b border-slate-800 hover:bg-slate-800/50">
                              {headers.map(h => (
                                <td key={h} className="px-4 py-3 text-slate-200">{String(row[h])}</td>
                              ))}
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    );
                  } catch {
                    return <div className="p-4 text-slate-500">Dati non parsabili</div>;
                  }
                })()}
              </div>
            </div>

            {/* Grafico (opzionale, sotto la tabella) */}
            {result.visualizationType !== 'table' && chartData.length > 0 && (
              <div className="mt-6">
                <p className="text-slate-400 text-sm mb-3">📈 Grafico:</p>
                <div className="bg-slate-900 border border-slate-700 p-4 rounded-lg h-64">
                  {result.visualizationType === 'bar' ? (
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart data={chartData}>
                        <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
                        <XAxis dataKey="name" stroke="#94a3b8" />
                        <YAxis stroke="#94a3b8" />
                        <Tooltip contentStyle={{ backgroundColor: '#1e293b', border: '1px solid #334155', color: '#f8fafc' }} />
                        <Bar dataKey="value" fill="#3b82f6" radius={[4, 4, 0, 0]} />
                      </BarChart>
                    </ResponsiveContainer>
                  ) : (
                    <ResponsiveContainer width="100%" height="100%">
                      <LineChart data={chartData}>
                        <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
                        <XAxis dataKey="name" stroke="#94a3b8" />
                        <YAxis stroke="#94a3b8" />
                        <Tooltip contentStyle={{ backgroundColor: '#1e293b', border: '1px solid #334155', color: '#f8fafc' }} />
                        <Line type="monotone" dataKey="value" stroke="#8b5cf6" strokeWidth={3} />
                      </LineChart>
                    </ResponsiveContainer>
                  )}
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

export default App;