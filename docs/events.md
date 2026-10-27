---
layout: catalog
title: 特殊イベント
description: Slafight Pluginの特殊イベント一覧。
permalink: /events/
code: EVNT
lead: 通常ラウンドとは違う流れになる特殊イベントの概要です。発生条件、目的、主な展開を確認できます。
catalog:
  data: events
  group: status
  placeholder: 例 Warhead, Raid, 季節
  fields:
    - { key: requirement, label: 表示条件 }
    - { key: min_players, label: 最低人数, suffix: 人 }
    - { key: season, label: 季節条件 }
    - { key: win_condition, label: 勝利 / 目的, wide: true }
  lists:
    - { key: flow, label: 主な流れ }
    - { key: notes, label: 注意 }
---
