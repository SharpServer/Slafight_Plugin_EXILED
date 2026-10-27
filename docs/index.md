---
layout: default
title: ホーム
description: Slafight Plugin EXILEDのユーザー向けWiki。
---
<header class="doc-head home-head">
  <p class="doc-meta">
    <span>SLAFIGHT FACILITY DATABASE</span>
    <span>{{ site.data.wiki.version }}</span>
    <span>CLEARANCE: PUBLIC</span>
  </p>
  <h1>Slafight Plugin Wiki</h1>
  <p class="lead">シャープ鯖（SCP: Secret Laboratory / EXILED）で追加されているロール、アイテム、特殊イベント、マップギミックの公開資料です。</p>
</header>

<aside class="notice">
  <strong>ACCESS</strong>
  <span>上部の検索（<kbd>/</kbd> キー）から全項目を横断検索できます。各項目の番号をクリックするとその項目へのリンクになります。</span>
</aside>

<section class="index-section">
  <h2 class="group-title">資料目録</h2>
  <ul class="index-list">
    {% for item in site.data.wiki.nav %}
    <li>
      <a href="{{ item.url | relative_url }}">
        <span class="index-code">{{ item.code }}</span>
        <span class="index-body">
          <strong>{{ item.title }}</strong>
          <span>{{ item.desc }}</span>
        </span>
        {% if item.data %}<span class="index-count">{{ site.data[item.data] | size }}<small>件</small></span>{% endif %}
      </a>
    </li>
    {% endfor %}
  </ul>
</section>
