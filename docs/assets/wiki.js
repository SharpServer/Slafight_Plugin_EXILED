(function () {
  const base = document.body.dataset.baseurl || "";
  const root = document.documentElement;

  const normalize = (value) => (value || "").toString().normalize("NFKC").toLowerCase().replace(/\s+/g, " ").trim();
  const escapeHtml = (value) => (value || "").toString()
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");

  // ===== テーマ切替 =====
  const themeButton = document.querySelector("[data-theme-toggle]");
  if (themeButton) {
    themeButton.addEventListener("click", () => {
      const current = root.getAttribute("data-theme")
        || (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
      const next = current === "dark" ? "light" : "dark";
      root.setAttribute("data-theme", next);
      try { localStorage.setItem("wiki-theme", next); } catch (e) { /* 保存できなくても切替は効く */ }
    });
  }

  // ===== モバイル用サイドバー =====
  const navToggle = document.querySelector("[data-nav-toggle]");
  const scrim = document.querySelector("[data-scrim]");
  const setNav = (open) => {
    document.body.classList.toggle("nav-open", open);
    if (navToggle) navToggle.setAttribute("aria-expanded", String(open));
    if (scrim) scrim.hidden = !open;
  };
  if (navToggle) navToggle.addEventListener("click", () => setNav(!document.body.classList.contains("nav-open")));
  if (scrim) scrim.addEventListener("click", () => setNav(false));

  // ===== 全体検索 =====
  const input = document.querySelector("[data-global-search]");
  const box = document.querySelector("[data-search-results]");
  let index = null;
  let hits = [];
  let selected = -1;

  const loadIndex = () => {
    if (index) return Promise.resolve(index);
    return fetch(`${base}/search.json`)
      .then((r) => (r.ok ? r.json() : []))
      .catch(() => [])
      .then((data) => {
        index = (Array.isArray(data) ? data : []).map((e) => ({
          ...e,
          _title: normalize(e.title),
          _all: normalize([e.title, e.type, e.group, e.summary, e.tags].join(" ")),
        }));
        return index;
      });
  };

  const score = (entry, needle) => {
    if (entry._title === needle) return 0;
    if (entry._title.startsWith(needle)) return 1;
    if (entry._title.includes(needle)) return 2;
    return 3;
  };

  const closeResults = () => {
    if (!box) return;
    box.hidden = true;
    input.setAttribute("aria-expanded", "false");
    selected = -1;
  };

  const select = (i) => {
    const links = box.querySelectorAll(".search-hit");
    links.forEach((el, n) => el.setAttribute("aria-selected", String(n === i)));
    selected = i;
    if (links[i]) links[i].scrollIntoView({ block: "nearest" });
  };

  const render = () => {
    const needle = normalize(input.value);
    if (!needle) {
      closeResults();
      return;
    }
    const terms = needle.split(" ");
    hits = index
      .filter((e) => terms.every((t) => e._all.includes(t)))
      .map((e) => ({ e, s: score(e, needle) }))
      .sort((a, b) => a.s - b.s)
      .slice(0, 30)
      .map((x) => x.e);

    box.innerHTML = hits.length
      ? hits.map((h, i) => `
        <a class="search-hit" role="option" aria-selected="false" data-index="${i}" href="${base}${h.url}">
          <span class="hit-code">${escapeHtml(h.code)}</span>
          <strong>${escapeHtml(h.title)}</strong>
          <small>${escapeHtml(h.type)}${h.group ? ` / ${escapeHtml(h.group)}` : ""}</small>
        </a>`).join("")
      : '<p class="search-empty">該当なし。語句を短くしてみてください。</p>';
    box.hidden = false;
    input.setAttribute("aria-expanded", "true");
    selected = -1;
  };

  if (input && box) {
    input.addEventListener("focus", () => loadIndex().then(render), { once: false });
    input.addEventListener("input", () => loadIndex().then(render));
    input.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        input.value = "";
        closeResults();
        input.blur();
        return;
      }
      if (box.hidden || !hits.length) return;
      if (event.key === "ArrowDown") {
        event.preventDefault();
        select((selected + 1) % hits.length);
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        select((selected - 1 + hits.length) % hits.length);
      } else if (event.key === "Enter") {
        event.preventDefault();
        const target = hits[Math.max(selected, 0)];
        closeResults();
        window.location.href = `${base}${target.url}`;
      }
    });
    box.addEventListener("click", closeResults);
    document.addEventListener("click", (event) => {
      if (!event.target.closest(".search")) closeResults();
    });
  }

  // "/" で検索欄へ
  document.addEventListener("keydown", (event) => {
    if (event.key !== "/" || event.ctrlKey || event.metaKey || event.altKey) return;
    const tag = (document.activeElement && document.activeElement.tagName) || "";
    if (tag === "INPUT" || tag === "TEXTAREA" || document.activeElement.isContentEditable) return;
    if (!input) return;
    event.preventDefault();
    input.focus();
    input.select();
  });

  // ===== ページ内絞り込み =====
  document.querySelectorAll("[data-filter-input]").forEach((filter) => {
    const scope = document.querySelector(filter.getAttribute("data-filter-scope")) || document;
    const cards = Array.from(scope.querySelectorAll("[data-card]")).map((card) => ({
      card,
      text: normalize(card.getAttribute("data-search") || card.textContent),
    }));
    const groups = Array.from(scope.querySelectorAll("[data-group]"));
    const counter = scope.querySelector("[data-filter-count]");
    const empty = scope.querySelector("[data-filter-empty]");
    const total = cards.length;

    const apply = () => {
      const terms = normalize(filter.value).split(" ").filter(Boolean);
      let shown = 0;
      cards.forEach(({ card, text }) => {
        const visible = terms.every((t) => text.includes(t));
        card.hidden = !visible;
        if (visible) shown += 1;
      });
      groups.forEach((group) => {
        const count = group.querySelectorAll("[data-card]:not([hidden])").length;
        group.hidden = count === 0;
        const badge = group.querySelector("[data-group-count]");
        if (badge) badge.textContent = count;
        const tocItem = document.querySelector(`[data-toc-for="${group.id}"]`);
        if (tocItem) tocItem.hidden = count === 0;
      });
      if (counter) counter.textContent = terms.length ? `${shown} / ${total} 件` : `${total} 件`;
      if (empty) empty.hidden = shown > 0;

      // ?q= に反映して共有できるようにする
      const url = new URL(window.location.href);
      if (terms.length) url.searchParams.set("q", filter.value.trim());
      else url.searchParams.delete("q");
      history.replaceState(null, "", url);
    };

    const initial = new URLSearchParams(window.location.search).get("q");
    if (initial) {
      filter.value = initial;
      apply();
    }
    filter.addEventListener("input", apply);
  });

  // ===== 目次の現在位置 =====
  const tocLinks = Array.from(document.querySelectorAll(".toc a"));
  if (tocLinks.length && "IntersectionObserver" in window) {
    const byId = new Map(tocLinks.map((a) => [a.getAttribute("href").slice(1), a]));
    const visible = new Set();
    const observer = new IntersectionObserver((records) => {
      records.forEach((r) => (r.isIntersecting ? visible.add(r.target.id) : visible.delete(r.target.id)));
      const firstVisible = Array.from(byId.keys()).find((id) => visible.has(id));
      if (!firstVisible) return;
      tocLinks.forEach((a) => a.classList.toggle("is-active", a === byId.get(firstVisible)));
    }, { rootMargin: "-120px 0px -50% 0px" });
    byId.forEach((_, id) => {
      const el = document.getElementById(id);
      if (el) observer.observe(el);
    });
  }

  // 絞り込み中に別項目へのリンクを踏んだら、その項目が見えるように解除する
  window.addEventListener("hashchange", () => {
    const target = document.getElementById(decodeURIComponent(window.location.hash.slice(1)));
    if (!target || !target.closest("[hidden]")) return;
    const filter = document.querySelector("[data-filter-input]");
    if (!filter) return;
    filter.value = "";
    filter.dispatchEvent(new Event("input"));
    target.scrollIntoView();
  });
})();
