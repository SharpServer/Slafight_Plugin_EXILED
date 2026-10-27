---
title: 内部管理ガイド
description: Wikiの内部管理ガイド。
permalink: /internal/
---

# 内部管理ガイド

公開Wikiはユーザー向け説明に絞り、更新作業やデータ構造の話はここに集約します。

## GitHub Pages設定

1. GitHubのリポジトリ設定を開く。
2. **Pages** を開く。
3. **Build and deployment** のSourceを **Deploy from a branch** にする。
4. Branchは **master**、Folderは **/docs** を選ぶ。
5. 保存後、数分待つとPages URLが発行される。

`docs/_config.yml` はリポジトリ名に合わせて `baseurl: "/Slafight_Plugin_EXILED"` にしています。リポジトリ名が違う場合やカスタムドメインで公開する場合はここを変更してください。

## 普段の更新フロー

| 更新したいもの | 編集ファイル | 反映先 |
| --- | --- | --- |
| カスタムロール | `docs/_data/roles.yml` | `/roles/` と検索 |
| カスタムアイテム | `docs/_data/items.yml` | `/items/` と検索 |
| 特殊イベント | `docs/_data/events.yml` | `/events/` と検索 |
| マップ / ギミック | `docs/_data/maps.yml` | `/maps/` と検索 |
| アビリティ | `docs/_data/abilities.yml` | `/abilities/` と検索 |
| キー設定 | `docs/_data/server_specifics.yml` | `/server-specifics/` と検索 |
| クレジット | `docs/_data/credits.yml` | `/credits/` と検索 |
| 内部用の共通仕様メモ | `docs/_data/mechanics.yml` | `/internal/mechanics/` |
| サイドバー・ホーム目次・バージョン | `docs/_data/wiki.yml` | 全ページ |

## ページの仕組み

一覧ページは `_layouts/catalog.html` が `_data` から自動生成します。各ページ（`roles.md` など）は front matter の `catalog:` だけを持ちます。

| キー | 意味 |
| --- | --- |
| `data` | `_data` のファイル名 |
| `group` | 見出しにする項目。省略すると1グループ（見出しは `group_title`） |
| `label` / `badge` | 項目番号の横に出す小ラベル / バージョン等のバッジ |
| `title` / `summary` | タイトル・概要に使う項目（既定 `name` / `summary`） |
| `fields` | 表形式で出す項目。`wide: true` で全幅、`suffix` で単位 |
| `lists` | 箇条書きで出す配列。`style: tags` でチップ、`style: links` でリンク |
| `search: false` | 全体検索（`search.json`）に載せない |

YAMLに新しい項目を足したときは、表示したいページの `fields` か `lists` にも追加してください。Markdownだけのページは `_layouts/page.html` で本文として表示されます。

## ロール追加テンプレート

`docs/_data/roles.yml` の該当陣営の位置に追加します。

```yaml
- id: example-role
  name: Example Role
  side: The Foundation
  team: 機動部隊
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
    - example
```

## イベント更新時の注意

イベントはプレイヤーに見せる条件と、コード上の実行可能条件がズレることがあります。最低限、次の3点を同時に確認してください。

1. `SpecialEvents/Events/*.cs` の `LocalizedName` と `TriggerRequirement`。
2. `MinPlayersRequired`。
3. `IsReadyToExecute()` の季節条件や無効化条件。

## ローカル確認

RubyとBundlerが入っている環境なら、以下で確認できます。

```sh
cd docs
bundle install
bundle exec jekyll serve
```

GitHub Pages上ではGitHub側がJekyllを実行します。ローカルにJekyllが無くても、コミットすれば公開されます。
