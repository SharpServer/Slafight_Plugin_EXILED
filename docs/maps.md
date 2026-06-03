---
layout: default
title: マップとギミック
description: Slafight Pluginの追加構造物と施設ギミック一覧。
permalink: /maps/
---
<section class="page-hero">
  <div>
    <p class="eyebrow">Maps / Gimmicks</p>
    <h1>マップとギミック</h1>
    <p>追加部屋、施設改修、SCP-106関連、SCP-330変更、弾頭演出などをゾーン別に整理しています。入口や権限の説明をここに集約します。</p>
  </div>
  <div class="filter-box">
    <label for="map-filter">このページを絞り込み</label>
    <input id="map-filter" data-filter-input data-filter-scope="#maps-list" type="search" placeholder="例: 拡張下層, 3005, Gate A">
  </div>
</section>

<div id="maps-list">
{% assign map_groups = site.data.maps | group_by: "zone" %}
{% for group in map_groups %}
<section class="content-section" data-group>
  <h2>{{ group.name }}</h2>
  <div class="card-grid">
  {% for entry in group.items %}
    <article class="wiki-card" id="{{ entry.id }}" data-card data-search="{{ entry.name }} {{ entry.zone }} {{ entry.entrance }} {{ entry.summary }} {{ entry.tags | join: ' ' }}">
      <header>
        <p class="eyebrow">{{ entry.kind | default: group.name }}</p>
        <h3>{{ entry.name }}</h3>
      </header>
      <p>{{ entry.summary }}</p>
      <dl class="meta-grid">
        {% if entry.entrance %}<div><dt>入口</dt><dd>{{ entry.entrance }}</dd></div>{% endif %}
        {% if entry.access %}<div><dt>権限 / 条件</dt><dd>{{ entry.access }}</dd></div>{% endif %}
        {% if entry.status %}<div><dt>状態</dt><dd>{{ entry.status }}</dd></div>{% endif %}
      </dl>
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
