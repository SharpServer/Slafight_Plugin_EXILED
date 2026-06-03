---
layout: default
title: アビリティ
description: キー設定から使うアビリティ一覧。
permalink: /abilities/
---
<section class="page-hero">
  <div>
    <p class="eyebrow">Abilities</p>
    <h1>アビリティ</h1>
    <p>一部SCPや特殊ロールが使用できる能力です。利用にはゲーム内キー設定の「アビリティ切り替え」と「アビリティ使用」の設定が必要です。</p>
  </div>
  <div class="filter-box">
    <label for="ability-filter">このページを絞り込み</label>
    <input id="ability-filter" data-filter-input data-filter-scope="#abilities-list" type="search" placeholder="例: SCP-035, シンクホール, 第五">
  </div>
</section>

<div id="abilities-list">
{% assign ability_groups = site.data.abilities | group_by: "category" %}
{% for group in ability_groups %}
<section class="content-section" data-group>
  <h2>{{ group.name }}</h2>
  <div class="card-grid">
  {% for ability in group.items %}
    {% capture ability_search %}{{ ability.name }} {{ ability.category }} {{ ability.users | join: ' ' }} {{ ability.summary }} {{ ability.tags | join: ' ' }}{% endcapture %}
    <article class="wiki-card" id="{{ ability.id }}" data-card data-search="{{ ability_search | strip_html | escape }}">
      <header>
        <p class="eyebrow">{{ ability.category }}</p>
        <h3>{{ ability.name }}</h3>
      </header>
      <p>{{ ability.summary }}</p>
      {% if ability.users %}
      <h4>主な利用者</h4>
      <ul class="tag-list">
        {% for user in ability.users %}<li>{{ user }}</li>{% endfor %}
      </ul>
      {% endif %}
      {% if ability.notes %}
      <h4>メモ</h4>
      <ul class="clean-list">
        {% for note in ability.notes %}<li>{{ note }}</li>{% endfor %}
      </ul>
      {% endif %}
    </article>
  {% endfor %}
  </div>
</section>
{% endfor %}
</div>
