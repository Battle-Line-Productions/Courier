"use client";

import { useState, useEffect, FormEvent } from "react";

const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

interface Job {
  id: string;
  name: string;
  description: string | null;
  currentVersion: number;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

interface ApiResponse<T> {
  data: T;
  success: boolean;
  timestamp: string;
  error: unknown;
}

interface PagedApiResponse<T> {
  data: T[];
  pagination: { page: number; pageSize: number; totalCount: number; totalPages: number };
  success: boolean;
  timestamp: string;
  error: unknown;
}

export default function Home() {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchJobs = async () => {
    try {
      const res = await fetch(`${API_URL}/api/v1/jobs`);
      const body: PagedApiResponse<Job> = await res.json();
      if (body.success) {
        setJobs(body.data);
      }
    } catch (err) {
      setError(`Failed to fetch jobs: ${err}`);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchJobs();
  }, []);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);

    try {
      const res = await fetch(`${API_URL}/api/v1/jobs`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name, description: description || null }),
      });

      const body: ApiResponse<Job> = await res.json();

      if (body.success) {
        setName("");
        setDescription("");
        fetchJobs();
      } else {
        setError(JSON.stringify(body.error));
      }
    } catch (err) {
      setError(`Failed to create job: ${err}`);
    }
  };

  return (
    <main>
      <h1>Courier — Jobs</h1>

      <section style={{ marginBottom: "2rem" }}>
        <h2>Create Job</h2>
        <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "0.5rem", maxWidth: 400 }}>
          <input
            type="text"
            placeholder="Job name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            style={{ padding: "0.5rem", border: "1px solid #ccc", borderRadius: 4 }}
          />
          <input
            type="text"
            placeholder="Description (optional)"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            style={{ padding: "0.5rem", border: "1px solid #ccc", borderRadius: 4 }}
          />
          <button type="submit" style={{ padding: "0.5rem 1rem", cursor: "pointer" }}>
            Create
          </button>
        </form>
      </section>

      {error && <p style={{ color: "red" }}>{error}</p>}

      <section>
        <h2>Jobs</h2>
        {loading ? (
          <p>Loading...</p>
        ) : jobs.length === 0 ? (
          <p>No jobs yet. Create one above.</p>
        ) : (
          <table style={{ borderCollapse: "collapse", width: "100%" }}>
            <thead>
              <tr>
                <th style={th}>Name</th>
                <th style={th}>Description</th>
                <th style={th}>Version</th>
                <th style={th}>Enabled</th>
                <th style={th}>Created</th>
              </tr>
            </thead>
            <tbody>
              {jobs.map((job) => (
                <tr key={job.id}>
                  <td style={td}>{job.name}</td>
                  <td style={td}>{job.description ?? "—"}</td>
                  <td style={td}>{job.currentVersion}</td>
                  <td style={td}>{job.isEnabled ? "Yes" : "No"}</td>
                  <td style={td}>{new Date(job.createdAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </main>
  );
}

const th: React.CSSProperties = {
  textAlign: "left",
  padding: "0.5rem",
  borderBottom: "2px solid #333",
};

const td: React.CSSProperties = {
  padding: "0.5rem",
  borderBottom: "1px solid #ddd",
};
