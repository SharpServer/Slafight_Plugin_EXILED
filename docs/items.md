---
layout: default
title: カスタムアイテム
description: Slafight Pluginのカスタムアイテム一覧。
permalink: /items/
---
<section class="page-hero">
  <div>
    <p class="eyebrow">Custom Items</p>
    <h1>カスタムアイテム</h1>
    <p>カード、武器、防具、回復アイテム、S-NAV、特殊ギミック系アイテムを分類しています。</p>
  </div>
  <div class="filter-box">
    <label for="item-filter">このページを絞り込み</label>
    <input id="item-filter" data-filter-input data-filter-scope="#items-list" type="search" placeholder="例: Serum, Railgun, キーカード">
  </div>
</section>

<div id="items-list">
{% assign item_groups = site.data.items | group_by: "category" %}
{% for group in item_groups %}
<section class="content-section" data-group>
  <h2>{{ group.name }}</h2>
  <div class="card-grid">
  {% for item in group.items %}
    {% capture item_search %}{{ item.name }} {{ item.category }} {{ item.base }} {{ item.summary }} {{ item.effect }} {{ item.stats }} {{ item.tags | join: ' ' }}{% endcapture %}
    <article class="wiki-card" id="{{ item.id }}" data-card data-search="{{ item_search | strip_html | escape }}">
      <header>
        <p class="eyebrow">{{ item.base | default: item.category }}{% if item.version %} / {{ item.version }}{% endif %}</p>
        <h3>{{ item.name }}</h3>
      </header>
      <p>{{ item.summary }}</p>
      <dl class="meta-grid">
        {% if item.base %}<div><dt>ベース</dt><dd>{{ item.base }}</dd></div>{% endif %}
        {% if item.permission %}<div><dt>権限</dt><dd>{{ item.permission }}</dd></div>{% endif %}
        {% if item.stats %}<div><dt>性能</dt><dd>{{ item.stats }}</dd></div>{% endif %}
        {% if item.status %}<div><dt>状態</dt><dd>{{ item.status }}</dd></div>{% endif %}
      </dl>
      {% if item.effect %}<p><strong>効果:</strong> {{ item.effect }}</p>{% endif %}
      {% if item.notes %}
      <h4>メモ</h4>
      <ul class="clean-list">
        {% for note in item.notes %}<li>{{ note }}</li>{% endfor %}
      </ul>
      {% endif %}
    </article>
  {% endfor %}
  </div>
</section>
{% endfor %}
</div>
