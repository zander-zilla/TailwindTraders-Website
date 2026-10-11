import React, { useState } from "react";
import { useNavigate } from "react-router-dom";

// Header search box (Phase 8): free-text, semantic search over the catalog (pgvector).
export default function SearchBox() {
  const [q, setQ] = useState("");
  const navigate = useNavigate();
  const submit = (e) => {
    e.preventDefault();
    const query = q.trim();
    if (query) navigate(`/search?q=${encodeURIComponent(query)}`);
  };
  return (
    <form className="search-box" role="search" onSubmit={submit}>
      <input
        className="search-box__input"
        type="search"
        name="q"
        aria-label="Search products"
        placeholder="Search: e.g. something to water plants"
        value={q}
        maxLength={200}
        onChange={(e) => setQ(e.target.value)}
      />
    </form>
  );
}
