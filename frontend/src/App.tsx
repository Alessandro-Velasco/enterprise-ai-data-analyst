import { useState, useEffect } from 'react';
import axios from 'axios';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, LineChart, Line, PieChart, Pie, Cell } from 'recharts';
import toast, { Toaster } from 'react-hot-toast';
import { saveAs } from 'file-saver';

const API_URL = 'http://localhost:5000/api/DataAnalysis';

interface Dataset {
  id: string;
  name: string;
  fileType: string;
}

interface QueryResult {
  id: string;
  generatedSQL: string;
  resultJSON: string;
  visualizationType: string;
  userQuery: string;
  executedAt: string;
}

const EXAMPLE_QUERIES = [
  "Mostrami tutte le righe",
  "Quanti laptop sono stati venduti?",
  "Mostra il totale delle quantità per città",
  "Qual è il prodotto più venduto?",
  "Mostra le vendite con totale superiore a 500"
];

const COLORS = ['#3b82f6', '#8b5cf6', '#10b981', '#f59e0b', '#ef4444', '#ec4899'];

function App() {
  const [datasets, setDatasets] = useState<Dataset[]>([]);
  const [selectedDataset, setSelectedDataset] = useState<string>('');
  const [query, setQuery] = useState('');
  const [result, setResult] = useState<QueryResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [queryHistory, setQueryHistory] = useState<QueryResult[]>([]);

  useEffect(() => {
    loadDatasets();
  }, []);

  const loadDatasets = async () => {
    try {
      const res = await axios.get(`${API_URL}/datasets`);
      setDatasets(res.data);
    } catch (err) {
      toast.error('Errore nel caricamento dei dataset');
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
      toast.success('✅ Dataset caricato con successo!');
      loadDatasets();
      e.target.value = '';
    } catch (err) {
      toast.error('❌ Errore nel caricamento del file');
    } finally {
      setUploading(false);
    }
  };

  const executeQuery = async () => {
    if (!selectedDataset || !query) {
      toast.error('Seleziona un dataset e scrivi una domanda!');
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
      setQueryHistory(prev => [res.data, ...prev].slice(0, 10)); // Salva ultime 10 query
      toast.success('Query eseguita con successo!');
    } catch (err) {
      toast.error('❌ Errore nell\'esecuzione della query');
    } finally {
      setLoading(false);
    }
  };

  const exportToCSV = () => {
    if (!result) return;
    try {
      const data = JSON.parse(result.resultJSON);
      if (data.error || !Array.isArray(data) || data.length === 0) {
        toast.error('Nessun dato da esportare');
        return;
      }
      const headers = Object.keys(data[0]);
      const csvContent = [
        headers.join(','),
        ...data.map((row: any) => headers.map(h => `"${row[h]}"`).join(','))
      ].join('\n');
      
      const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
      saveAs(blob, `query_result_${Date.now()}.csv`);
      toast.success('CSV esportato!');
    } catch {
      toast.error('Errore nell\'export');
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

  const chartData = (() => {
  const data = getParsedData();
  if (data.length === 0) return [];
  
  const keys = Object.keys(data[0]);
  
  // Trova automaticamente una colonna numerica per il valore
  const numericKey = keys.find(k => !isNaN(parseFloat(data[0][k]))) || keys[keys.length - 1];
  
  // Trova una colonna testuale per l'etichetta (preferibilmente Citta, Prodotto, Data)
  const labelKey = keys.find(k => 
    k.toLowerCase().includes('citta') || 
    k.toLowerCase().includes('prodotto') || 
    k.toLowerCase().includes('data') ||
    k !== numericKey
  ) || keys[0];
  
  return data.map((row: any) => ({
    name: String(row[labelKey]),
    value: parseFloat(row[numericKey]) || 0
  }));
})();

  return (
    <div className="min-h-screen bg-slate-900 text-slate-50 p-6">
      <Toaster position="top-right" toastOptions={{
        style: { background: '#1e293b', color: '#f8fafc', border: '1px solid #334155' }
      }} />
      
      <div className="max-w-7xl mx-auto">
        {/* Header */}
        <header className="mb-8 border-b border-slate-700 pb-4">
          <h1 className="text-3xl font-bold text-blue-400 flex items-center gap-3">
            🗄️ Enterprise AI Data Analyst
          </h1>
          <p className="text-slate-400 mt-2">Analizza i tuoi dati CSV con l'intelligenza artificiale locale (Ollama)</p>
        </header>

        <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
          {/* Sidebar: Query History */}
          <div className="lg:col-span-1">
            <div className="bg-slate-800 p-6 rounded-xl border border-slate-700 shadow-lg sticky top-6">
              <h2 className="text-lg font-bold mb-4 text-slate-300">📜 Storico Query</h2>
              {queryHistory.length === 0 ? (
                <p className="text-slate-500 text-sm">Nessuna query eseguita</p>
              ) : (
                <div className="space-y-2 max-h-96 overflow-y-auto">
                  {queryHistory.map((q, idx) => (
                    <div key={idx} className="bg-slate-900 p-3 rounded-lg border border-slate-700 text-xs">
                      <p className="text-slate-300 truncate">{q.userQuery}</p>
                      <p className="text-slate-500 mt-1">{new Date(q.executedAt).toLocaleTimeString()}</p>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>

          {/* Main Content */}
          <div className="lg:col-span-3 space-y-6">
            {/* 3 Cards Grid */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              
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
                  {loading ? (
                    <>
                      <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                      Analisi in corso...
                    </>
                  ) : '🚀 Esegui Query AI'}
                </button>
              </div>
            </div>

            {/* Example Queries */}
            <div className="bg-slate-800 p-6 rounded-xl border border-slate-700 shadow-lg">
              <h2 className="text-lg font-bold mb-4 text-slate-300">💡 Esempi di Query</h2>
              <div className="flex flex-wrap gap-2">
                {EXAMPLE_QUERIES.map((example, idx) => (
                  <button
                    key={idx}
                    onClick={() => setQuery(example)}
                    className="bg-slate-900 hover:bg-slate-700 border border-slate-600 text-slate-300 px-4 py-2 rounded-lg text-sm transition"
                  >
                    {example}
                  </button>
                ))}
              </div>
            </div>

            {/* Results Section */}
            {loading ? (
              <div className="bg-slate-800 p-6 rounded-xl border border-slate-700 shadow-lg animate-pulse">
                <div className="h-8 bg-slate-700 rounded w-1/3 mb-4"></div>
                <div className="h-4 bg-slate-700 rounded w-1/2 mb-2"></div>
                <div className="h-32 bg-slate-700 rounded"></div>
              </div>
            ) : result ? (
              <div className="bg-slate-800 p-6 rounded-xl border border-slate-700 shadow-lg">
                <div className="flex justify-between items-center mb-4">
                  <h2 className="text-xl font-bold text-green-400">💻 Risultato Analisi</h2>
                  <button
                    onClick={exportToCSV}
                    className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg text-sm font-semibold transition"
                  >
                    📥 Export CSV
                  </button>
                </div>
                
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
                  <div>
                    <p className="text-slate-400 text-sm mb-2">Query SQL Generata:</p>
                    <code className="block bg-slate-900 border border-slate-700 p-4 rounded-lg text-green-400 font-mono text-sm overflow-x-auto">
                      {result.generatedSQL}
                    </code>
                  </div>

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

                {/* Grafico */}
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
                      ) : result.visualizationType === 'pie' ? (
                        <ResponsiveContainer width="100%" height="100%">
                          <PieChart>
                            <Pie data={chartData} cx="50%" cy="50%" outerRadius={80} fill="#8884d8" dataKey="value" label>
                              {chartData.map((entry, index) => (
                                <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                              ))}
                            </Pie>
                            <Tooltip contentStyle={{ backgroundColor: '#1e293b', border: '1px solid #334155', color: '#f8fafc' }} />
                          </PieChart>
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
            ) : null}
          </div>
        </div>
      </div>
    </div>
  );
}

export default App;