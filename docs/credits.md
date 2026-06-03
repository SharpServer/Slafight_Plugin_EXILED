---
layout: default
title: クレジット
description: Slafight Plugin Wikiのクレジットとライセンス整理。
permalink: /credits/
---
<section class="page-hero">
  <div>
    <p class="eyebrow">Credits / Licenses</p>
    <h1>クレジット</h1>
    <p>コンテンツや素材のクレジットは本文から分離して管理します。新しいBGMや素材を追加したら <code>docs/_data/credits.yml</code> に追記してください。</p>
  </div>
  <div class="filter-box">
    <label for="credit-filter">このページを絞り込み</label>
    <input id="credit-filter" data-filter-input data-filter-scope="#credits-list" type="search" placeholder="例: CC BY-SA, Warhead, Logo">
  </div>
</section>

<div id="credits-list">
{% assign credit_groups = site.data.credits | group_by: "category" %}
{% for group in credit_groups %}
<section class="content-section" data-group>
  <h2>{{ group.name }}</h2>
  <div class="card-grid">
  {% for credit in group.items %}
    <article class="wiki-card" id="{{ credit.id }}" data-card data-search="{{ credit.title }} {{ credit.author }} {{ credit.license }} {{ credit.usage }} {{ credit.category }}">
      <header>
        <p class="eyebrow">{{ credit.license | default: group.name }}</p>
        <h3>{{ credit.title }}</h3>
      </header>
      {% if credit.usage %}<p>{{ credit.usage }}</p>{% endif %}
      <dl class="meta-grid">
        {% if credit.author %}<div><dt>作者</dt><dd>{{ credit.author }}</dd></div>{% endif %}
        {% if credit.license %}<div><dt>ライセンス</dt><dd>{{ credit.license }}</dd></div>{% endif %}
      </dl>
      {% if credit.links %}
      <h4>リンク</h4>
      <ul class="clean-list">
        {% for link in credit.links %}<li><a href="{{ link }}">{{ link }}</a></li>{% endfor %}
      </ul>
      {% endif %}
      {% if credit.notes %}
      <h4>メモ</h4>
      <ul class="clean-list">
        {% for note in credit.notes %}<li>{{ note }}</li>{% endfor %}
      </ul>
      {% endif %}
    </article>
  {% endfor %}
  </div>
</section>
{% endfor %}
</div>
