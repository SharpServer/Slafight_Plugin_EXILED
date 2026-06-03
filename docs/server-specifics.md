---
layout: default
title: キー設定
description: ゲーム内キー設定一覧。
permalink: /server-specifics/
---
<section class="page-hero">
  <div>
    <p class="eyebrow">Controls</p>
    <h1>キー設定</h1>
    <p>ゲーム内の「設定 → Server Specifics」から設定する項目です。プレイヤー向け案内としてそのままDiscordやゲーム内告知に流用できます。</p>
  </div>
  <div class="filter-box">
    <label for="specific-filter">このページを絞り込み</label>
    <input id="specific-filter" data-filter-input data-filter-scope="#specifics-list" type="search" placeholder="例: 近接, アビリティ, コード">
  </div>
</section>

<section id="specifics-list" class="content-section" data-group>
  <h2>設定項目</h2>
  <div class="card-grid">
  {% for entry in site.data.server_specifics %}
    {% capture specific_search %}{{ entry.name }} {{ entry.default }} {{ entry.recommended }} {{ entry.summary }} {{ entry.tags | join: ' ' }}{% endcapture %}
    <article class="wiki-card" id="{{ entry.id }}" data-card data-search="{{ specific_search | strip_html | escape }}">
      <header>
        <p class="eyebrow">{{ entry.input_type }}</p>
        <h3>{{ entry.name }}</h3>
      </header>
      <p>{{ entry.summary }}</p>
      <dl class="meta-grid">
        {% if entry.recommended %}<div><dt>推奨</dt><dd>{{ entry.recommended }}</dd></div>{% endif %}
        {% if entry.default %}<div><dt>デフォルト</dt><dd>{{ entry.default }}</dd></div>{% endif %}
        {% if entry.usage %}<div><dt>使い方</dt><dd>{{ entry.usage }}</dd></div>{% endif %}
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
