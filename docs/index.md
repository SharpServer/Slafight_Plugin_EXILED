---
layout: default
title: ホーム
description: Slafight Plugin EXILEDのユーザー向けWiki。
---
<section class="hero">
  <p class="hero-kicker">SCP: Secret Laboratory / EXILED</p>
  <h1>Slafight Plugin Wiki</h1>
  <p>シャープ鯖で追加されているカスタムロール、アイテム、特殊イベント、マップギミックをまとめたユーザー向けWikiです。気になる要素は上の検索かカテゴリから確認できます。</p>
  <div class="hero-actions">
    <a class="button" href="{{ '/roles/' | relative_url }}">ロールを見る</a>
    <a class="button secondary" href="{{ '/items/' | relative_url }}">アイテムを見る</a>
  </div>
</section>

<section class="content-section">
  <h2>まず見るカテゴリ</h2>
  <div class="section-grid">
    <a class="wiki-card" href="{{ '/roles/' | relative_url }}" data-card data-search="roles custom role scp mtf chaos goc">
      <p class="eyebrow">{{ site.data.roles | size }} entries</p>
      <h3>カスタムロール</h3>
      <p>SCP、財団、GoI、警備員、科学者、Dクラス系の追加ロールをカード形式で整理。</p>
    </a>
    <a class="wiki-card" href="{{ '/items/' | relative_url }}" data-card data-search="items weapons keycards armor serum snav">
      <p class="eyebrow">{{ site.data.items | size }} entries</p>
      <h3>カスタムアイテム</h3>
      <p>キーカード、武器、防具、血清、S-NAV、アノマリー由来アイテムを分類。</p>
    </a>
    <a class="wiki-card" href="{{ '/events/' | relative_url }}" data-card data-search="events special raid warhead seasonal">
      <p class="eyebrow">{{ site.data.events | size }} entries</p>
      <h3>特殊イベント</h3>
      <p>抽選イベント、季節イベント、弾頭系イベントの条件・目的・注意点を整理。</p>
    </a>
    <a class="wiki-card" href="{{ '/maps/' | relative_url }}" data-card data-search="maps rooms gimmicks facility">
      <p class="eyebrow">{{ site.data.maps | size }} entries</p>
      <h3>マップ / ギミック</h3>
      <p>追加部屋、施設変更、SCP-106・SCP-330・弾頭演出などをゾーン別に表示。</p>
    </a>
  </div>
</section>

<section class="content-section">
  <h2>プレイに役立つ情報</h2>
  <div class="section-grid">
    <a class="wiki-card" href="{{ '/abilities/' | relative_url }}">
      <p class="eyebrow">Abilities</p>
      <h3>アビリティ</h3>
      <p>キー設定から使う特殊能力の効果と主な利用者。</p>
    </a>
    <a class="wiki-card" href="{{ '/server-specifics/' | relative_url }}">
      <p class="eyebrow">Controls</p>
      <h3>キー設定</h3>
      <p>近接チャット、キャラクター名、アビリティ切替/使用、シークレットコード。</p>
    </a>
    <a class="wiki-card" href="{{ '/credits/' | relative_url }}">
      <p class="eyebrow">License</p>
      <h3>クレジット</h3>
      <p>SCP Foundation、BGM、音声素材、ロゴなどのクレジットを分離管理。</p>
    </a>
  </div>
</section>
