---
layout: default
title: 特殊イベント
description: Slafight Pluginの特殊イベント一覧。
permalink: /events/
---
<section class="page-hero">
  <div>
    <p class="eyebrow">Special Events</p>
    <h1>特殊イベント</h1>
    <p>通常ラウンドとは違う流れになる特殊イベントの概要です。発生条件、目的、主な展開を確認できます。</p>
  </div>
  <div class="filter-box">
    <label for="event-filter">このページを絞り込み</label>
    <input id="event-filter" data-filter-input data-filter-scope="#events-list" type="search" placeholder="例: Warhead, Raid, 無効">
  </div>
</section>

<div id="events-list">
<section class="content-section" data-group>
  <h2>イベント一覧</h2>
  <div class="card-grid">
  {% for event in site.data.events %}
    {% capture event_search %}{{ event.name }} {{ event.event_type }} {{ event.status }} {{ event.requirement }} {{ event.summary }} {{ event.tags | join: ' ' }}{% endcapture %}
    <article class="wiki-card" id="{{ event.id }}" data-card data-search="{{ event_search | strip_html | escape }}">
      <header>
        <p class="eyebrow">{{ event.event_type }}{% if event.status %} / {{ event.status }}{% endif %}</p>
        <h3>{{ event.name }}</h3>
      </header>
      <p>{{ event.summary }}</p>
      <dl class="meta-grid">
        {% if event.requirement %}<div><dt>表示条件</dt><dd>{{ event.requirement }}</dd></div>{% endif %}
        {% if event.min_players != nil %}<div><dt>最低人数</dt><dd>{{ event.min_players }}人</dd></div>{% endif %}
        {% if event.season %}<div><dt>季節条件</dt><dd>{{ event.season }}</dd></div>{% endif %}
        {% if event.win_condition %}<div><dt>勝利 / 目的</dt><dd>{{ event.win_condition }}</dd></div>{% endif %}
      </dl>
      {% if event.flow %}
      <h4>主な流れ</h4>
      <ul class="clean-list">
        {% for step in event.flow %}<li>{{ step }}</li>{% endfor %}
      </ul>
      {% endif %}
      {% if event.notes %}
      <h4>注意</h4>
      <ul class="clean-list">
        {% for note in event.notes %}<li>{{ note }}</li>{% endfor %}
      </ul>
      {% endif %}
    </article>
  {% endfor %}
  </div>
</section>
</div>
