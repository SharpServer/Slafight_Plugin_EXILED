---
layout: catalog
title: カスタムアイテム
description: Slafight Pluginのカスタムアイテム一覧。
permalink: /items/
code: ITEM
lead: カード、武器、防具、回復アイテム、S-NAV、特殊ギミック系アイテムを分類しています。
catalog:
  data: items
  group: category
  label: base
  badge: version
  placeholder: 例 Serum, Railgun, キーカード
  fields:
    - { key: permission, label: 権限 }
    - { key: stats, label: 性能 }
    - { key: status, label: 状態 }
    - { key: effect, label: 効果, wide: true }
  lists:
    - { key: notes, label: メモ }
---
