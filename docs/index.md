---
layout: default
title: ホーム
description: Slafight Plugin EXILEDのユーザー向けWiki。
---
<section class="hero">
  <p class="hero-kicker">SCP: Secret Laboratory / EXILED</p>
  <h1>Slafight Plugin Wiki</h1>
  <p>Discordフォーラムに散らばりがちなカスタムロール、アイテム、特殊イベント、マップギミックをGitHub Pagesでまとめるためのデータ駆動Wikiです。追加や修正は <code>docs/_data/*.yml</code> を編集するだけで反映されます。</p>
  <div class="hero-actions">
    <a class="button" href="{{ '/roles/' | relative_url }}">ロールを見る</a>
    <a class="button secondary" href="{{ '/maintenance/' | relative_url }}">更新方法</a>
  </div>
</section>

<section class="content-section">
  <h2>主要カテゴリ</h2>
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
  <h2>運用向け</h2>
  <div class="section-grid">
    <a class="wiki-card" href="{{ '/mechanics/' | relative_url }}">
      <p class="eyebrow">EventHandler</p>
      <h3>共通仕様</h3>
      <p>ラウンド開始時の共通処理、ロビー表示、ゲートロック、SCP切断時の代替スポーンなど。</p>
    </a>
    <a class="wiki-card" href="{{ '/abilities/' | relative_url }}">
      <p class="eyebrow">Abilities</p>
      <h3>アビリティ</h3>
      <p>ServerSpecificキーから使う特殊能力の効果と主な利用者。</p>
    </a>
    <a class="wiki-card" href="{{ '/server-specifics/' | relative_url }}">
      <p class="eyebrow">Controls</p>
      <h3>ServerSpecific設定</h3>
      <p>近接チャット、キャラクター名、アビリティ切替/使用、シークレットコード。</p>
    </a>
    <a class="wiki-card" href="{{ '/credits/' | relative_url }}">
      <p class="eyebrow">License</p>
      <h3>クレジット</h3>
      <p>SCP Foundation、BGM、音声素材、ロゴなどのクレジットを分離管理。</p>
    </a>
  </div>
</section>

<section class="content-section">
  <h2>便利リンク</h2>
  <div class="section-grid">
    <a class="wiki-card" href="{{ '/scp914-keycard-flow.html' | relative_url }}">
      <p class="eyebrow">Reference</p>
      <h3>SCP-914 Keycard Flow</h3>
      <p>実装から整理したキーカード変換の早見表。既存HTMLをそのまま公開対象にしています。</p>
    </a>
    <a class="wiki-card" href="{{ '/maintenance/' | relative_url }}">
      <p class="eyebrow">Maintainer</p>
      <h3>追加・更新ルール</h3>
      <p>フォーラム投稿ではなくデータに追記していくためのテンプレートとGitHub Pages設定。</p>
    </a>
  </div>
</section>

