import React, { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Card, LoadingSpinner } from "../../shared";
import ProductService from "../../services/productsService";

// /search?q=...  -> GET /api/v1/products/search (semantic search, Phase 8)
export default function SearchResults() {
  const [params] = useSearchParams();
  const q = params.get("q") || "";
  const [state, setState] = useState({ loading: true, results: [], model: "", error: null });

  useEffect(() => {
    let cancelled = false;
    setState((s) => ({ ...s, loading: true, error: null }));
    ProductService.semanticSearch(q)
      .then((data) => !cancelled && setState({ loading: false, results: data.results, model: data.model, error: null }))
      .catch(() => !cancelled && setState({ loading: false, results: [], model: "", error: "Search failed, please try again." }));
    return () => {
      cancelled = true;
    };
  }, [q]);

  return (
    <div className="search-results">
      <h2 className="search-results__title">
        Results for “{q}”
        {state.model && <span className="search-results__model" title="embedding model">{state.model}</span>}
      </h2>
      {state.loading && <LoadingSpinner />}
      {state.error && <p className="search-results__empty">{state.error}</p>}
      {!state.loading && !state.error && state.results.length === 0 && (
        <p className="search-results__empty">No matching products. Try describing what you want to do.</p>
      )}
      <div className="suggestedproductslist">
        {state.results.map((p) => (
          <div className="search-results__item" key={p.id} data-score={p.score}>
            <Card id={p.id} imageUrl={p.imageUrl} name={p.name} price={p.price} />
            <span className="search-results__score">{Math.round(p.score * 100)}% match</span>
          </div>
        ))}
      </div>
    </div>
  );
}
