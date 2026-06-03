---
layout: default
title: カスタムロール
description: Slafight Pluginのカスタムロール一覧。
permalink: /roles/
---
<section class="page-hero">
  <div>
    <p class="eyebrow">Custom Roles</p>
    <h1>カスタムロール</h1>
    <p>プレイヤーが遭遇する独自ロールの説明、陣営、HP、主な所持品、出現条件をまとめています。</p>
  </div>
  <div class="filter-box">
    <label for="role-filter">このページを絞り込み</label>
    <input id="role-filter" data-filter-input data-filter-scope="#roles-list" type="search" placeholder="例: SCP-610, GoC, ハンマー">
  </div>
</section>

<div id="roles-list">
{% assign role_groups = site.data.roles | group_by: "side" %}
{% for group in role_groups %}
<section class="content-section" data-group>
  <h2>{{ group.name }}</h2>
  <div class="card-grid">
  {% for role in group.items %}
    {% capture role_search %}{{ role.name }} {{ role.side }} {{ role.team }} {{ role.base }} {{ role.summary }} {{ role.condition }} {{ role.items | join: ' ' }} {{ role.tags | join: ' ' }}{% endcapture %}
    <article class="wiki-card" id="{{ role.id }}" data-card data-search="{{ role_search | strip_html | escape }}">
      <header>
        <p class="eyebrow">{{ role.team | default: group.name }}{% if role.version %} / {{ role.version }}{% endif %}</p>
        <h3>{{ role.name }}</h3>
      </header>
      <p>{{ role.summary }}</p>
      <dl class="meta-grid">
        {% if role.base %}<div><dt>ベース</dt><dd>{{ role.base }}</dd></div>{% endif %}
        {% if role.hp %}<div><dt>HP</dt><dd>{{ role.hp }}</dd></div>{% endif %}
        {% if role.hume %}<div><dt>HS / AHP</dt><dd>{{ role.hume }}</dd></div>{% endif %}
        {% if role.spawn %}<div><dt>スポーン</dt><dd>{{ role.spawn }}</dd></div>{% endif %}
        {% if role.condition %}<div><dt>条件</dt><dd>{{ role.condition }}</dd></div>{% endif %}
      </dl>
      {% if role.items %}
      <h4>主な所持品</h4>
      <ul class="tag-list">
        {% for item in role.items %}<li>{{ item }}</li>{% endfor %}
      </ul>
      {% endif %}
      {% if role.abilities %}
      <h4>特性 / アビリティ</h4>
      <ul class="clean-list">
        {% for ability in role.abilities %}<li>{{ ability }}</li>{% endfor %}
      </ul>
      {% endif %}
      {% if role.notes %}
      <h4>メモ</h4>
      <ul class="clean-list">
        {% for note in role.notes %}<li>{{ note }}</li>{% endfor %}
      </ul>
      {% endif %}
    </article>
  {% endfor %}
  </div>
</section>
{% endfor %}
</div>
