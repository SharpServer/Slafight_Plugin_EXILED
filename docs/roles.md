---
layout: catalog
title: カスタムロール
description: Slafight Pluginのカスタムロール一覧。
permalink: /roles/
code: ROLE
lead: プレイヤーが遭遇する独自ロールの説明、陣営、HP、主な所持品、出現条件をまとめています。
catalog:
  data: roles
  group: side
  label: team
  badge: version
  placeholder: 例 SCP-610, GoC, ハンマー
  fields:
    - { key: base, label: ベース }
    - { key: hp, label: HP }
    - { key: hume, label: HS / AHP }
    - { key: spawn, label: スポーン }
    - { key: condition, label: 条件, wide: true }
  lists:
    - { key: items, label: 主な所持品, style: tags }
    - { key: abilities, label: 特性 / アビリティ }
    - { key: notes, label: メモ }
---
