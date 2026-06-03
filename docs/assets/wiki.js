(function () {
  const base = document.body.dataset.baseurl || "";
  const globalInput = document.querySelector("[data-global-search]");
  const resultsBox = document.querySelector("[data-search-results]");
  let searchIndex = [];

  const normalize = (value) => (value || "").toString().toLowerCase().replace(/\s+/g, " ").trim();
  const escapeHtml = (value) => (value || "").toString()
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");

  function renderSearch(query) {
    if (!globalInput || !resultsBox) return;
    const needle = normalize(query);
    if (needle.length < 2) {
      resultsBox.hidden = true;
      resultsBox.innerHTML = "";
      return;
    }

    const hits = searchIndex
      .filter((entry) => normalize([entry.title, entry.type, entry.group, entry.summary, entry.tags].join(" ")).includes(needle))
      .slice(0, 12);

    if (hits.length === 0) {
      resultsBox.hidden = false;
      resultsBox.innerHTML = '<div class="search-hit"><strong>該当なし</strong><span>表記ゆれを短くして検索してください。</span></div>';
      return;
    }

    resultsBox.hidden = false;
    resultsBox.innerHTML = hits.map((hit) => `
      <a class="search-hit" href="${base}${hit.url}">
        <strong>${escapeHtml(hit.title)}</strong>
        <span>${escapeHtml(hit.type)} / ${escapeHtml(hit.group || "未分類")}</span>
      </a>
    `).join("");
  }

  if (globalInput && resultsBox) {
    fetch(`${base}/search.json`)
      .then((response) => response.ok ? response.json() : [])
      .then((data) => { searchIndex = Array.isArray(data) ? data : []; })
      .catch(() => { searchIndex = []; });

    globalInput.addEventListener("input", (event) => renderSearch(event.target.value));
    globalInput.addEventListener("focus", (event) => renderSearch(event.target.value));
    document.addEventListener("click", (event) => {
      if (!event.target.closest(".global-search")) resultsBox.hidden = true;
    });
  }

  document.querySelectorAll("[data-filter-input]").forEach((input) => {
    const scopeSelector = input.getAttribute("data-filter-scope");
    const scope = scopeSelector ? document.querySelector(scopeSelector) : document;
    if (!scope) return;

    const cards = Array.from(scope.querySelectorAll("[data-card]"));
    const groups = Array.from(scope.querySelectorAll("[data-group]"));

    input.addEventListener("input", () => {
      const needle = normalize(input.value);
      cards.forEach((card) => {
        const haystack = normalize(card.getAttribute("data-search") || card.textContent);
        card.hidden = needle.length > 0 && !haystack.includes(needle);
      });

      groups.forEach((group) => {
        const visible = Array.from(group.querySelectorAll("[data-card]")).some((card) => !card.hidden);
        group.hidden = !visible;
      });
    });
  });
})();
