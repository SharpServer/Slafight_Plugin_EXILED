---
layout: default
title: 内部管理ガイド
description: Wikiの内部管理ガイド。
permalink: /internal/
---
<section class="page-hero">
  <div>
    <p class="eyebrow">Maintenance</p>
    <h1>内部管理ガイド</h1>
    <p>このページは管理者向けです。公開Wikiに出す内容はユーザー向け説明に絞り、更新作業やデータ構造の話はここに集約します。</p>
  </div>
</section>

<section class="content-section prose">
  <h2>GitHub Pages設定</h2>
  <ol>
    <li>GitHubのリポジトリ設定を開く。</li>
    <li><strong>Pages</strong> を開く。</li>
    <li><strong>Build and deployment</strong> のSourceを <strong>Deploy from a branch</strong> にする。</li>
    <li>Branchは <strong>main</strong>、Folderは <strong>/docs</strong> を選ぶ。</li>
    <li>保存後、数分待つとPages URLが発行される。</li>
  </ol>
  <p>現在の <code>docs/_config.yml</code> は、このリポジトリ名に合わせて <code>baseurl: "/Slafight_Plugin_EXILED"</code> にしています。GitHub上のリポジトリ名が違う場合、またはカスタムドメインで公開する場合はここを変更してください。</p>
  <p>GitHub Wikiでも運用はできますが、Wikiはデータファイルから一覧を自動生成しづらいです。この構成ではPagesを推奨します。</p>
</section>

<section class="content-section prose">
  <h2>普段の更新フロー</h2>
  <table>
    <thead><tr><th>更新したいもの</th><th>編集ファイル</th><th>反映先</th></tr></thead>
    <tbody>
      <tr><td>カスタムロール</td><td><code>docs/_data/roles.yml</code></td><td><code>/roles/</code> と検索</td></tr>
      <tr><td>カスタムアイテム</td><td><code>docs/_data/items.yml</code></td><td><code>/items/</code> と検索</td></tr>
      <tr><td>特殊イベント</td><td><code>docs/_data/events.yml</code></td><td><code>/events/</code> と検索</td></tr>
      <tr><td>マップ / ギミック</td><td><code>docs/_data/maps.yml</code></td><td><code>/maps/</code> と検索</td></tr>
      <tr><td>内部用の共通仕様メモ</td><td><code>docs/_data/mechanics.yml</code></td><td><code>/internal/mechanics/</code></td></tr>
      <tr><td>アビリティ</td><td><code>docs/_data/abilities.yml</code></td><td><code>/abilities/</code> と検索</td></tr>
      <tr><td>キー設定</td><td><code>docs/_data/server_specifics.yml</code></td><td><code>/server-specifics/</code> と検索</td></tr>
      <tr><td>クレジット</td><td><code>docs/_data/credits.yml</code></td><td><code>/credits/</code> と検索</td></tr>
    </tbody>
  </table>
</section>

<section class="content-section prose">
  <h2>ロール追加テンプレート</h2>
  <p><code>docs/_data/roles.yml</code> の末尾に追加します。</p>
  <pre><code>- id: example-role
  name: Example Role
  side: Foundation - Mobile Task Forces
  team: 財団
  base: MTF二等兵
  hp: 100
  version: v1.x.x
  condition: 特殊イベントで出現
  summary: ここにユーザー向け説明を書く。
  items:
    - E-11-SR
    - Medkit
  abilities:
    - 特殊な効果があれば書く
  notes:
    - 運用上の注意があれば書く
  tags:
    - mtf
    - example</code></pre>
</section>

<section class="content-section prose">
  <h2>イベント更新時の注意</h2>
  <p>イベントはプレイヤーに見せる条件と、コード上の実行可能条件がズレることがあります。最低限、次の3点を同時に確認してください。</p>
  <ol>
    <li><code>SpecialEvents/Events/*.cs</code> の <code>LocalizedName</code> と <code>TriggerRequirement</code>。</li>
    <li><code>MinPlayersRequired</code>。</li>
    <li><code>IsReadyToExecute()</code> の季節条件や無効化条件。</li>
  </ol>
</section>

<section class="content-section prose">
  <h2>ローカル確認</h2>
  <p>RubyとBundlerが入っている環境なら、以下で確認できます。</p>
  <pre><code>cd docs
bundle install
bundle exec jekyll serve</code></pre>
  <p>GitHub Pages上ではGitHub側がJekyllを実行します。ローカルにJekyllが無くても、コミットしてPagesを有効化すれば公開できます。</p>
</section>

