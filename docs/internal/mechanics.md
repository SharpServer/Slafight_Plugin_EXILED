---
layout: default
title: 内部用 共通仕様メモ
description: 管理者向けのEventHandler.cs由来メモ。
permalink: /internal/mechanics/
---
<section class="page-hero">
  <div>
    <p class="eyebrow">Global Mechanics</p>
    <h1>共通仕様</h1>
    <p><code>MainHandlers/EventHandler.cs</code> にある、全イベント・全ラウンドにまたがる仕様を管理者向けにまとめています。</p>
  </div>
  <div class="filter-box">
    <label for="mechanic-filter">このページを絞り込み</label>
    <input id="mechanic-filter" data-filter-input data-filter-scope="#mechanics-list" type="search" placeholder="例: Gate, SCP-500, ロビー">
  </div>
</section>

<div id="mechanics-list">
{% assign mechanic_groups = site.data.mechanics | group_by: "category" %}
{% for group in mechanic_groups %}
<section class="content-section" data-group>
  <h2>{{ group.name }}</h2>
  <div class="card-grid">
  {% for entry in group.items %}
    {% capture mechanic_search %}{{ entry.name }} {{ entry.category }} {{ entry.summary }} {{ entry.tags | join: ' ' }}{% endcapture %}
    <article class="wiki-card" id="{{ entry.id }}" data-card data-search="{{ mechanic_search | strip_html | escape }}">
      <header>
        <p class="eyebrow">{{ entry.source | default: group.name }}</p>
        <h3>{{ entry.name }}</h3>
      </header>
      <p>{{ entry.summary }}</p>
      {% if entry.details %}
      <h4>詳細</h4>
      <ul class="clean-list">
        {% for detail in entry.details %}<li>{{ detail }}</li>{% endfor %}
      </ul>
      {% endif %}
      {% if entry.notes %}
      <h4>メモ</h4>
      <ul class="clean-list">
        {% for note in entry.notes %}<li>{{ note }}</li>{% endfor %}
      </ul>
      {% endif %}
    </article>
  {% endfor %}
  </div>
</section>
{% endfor %}
</div>

