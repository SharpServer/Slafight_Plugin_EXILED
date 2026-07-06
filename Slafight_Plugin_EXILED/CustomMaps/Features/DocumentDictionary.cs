using System;

namespace Slafight_Plugin_EXILED.CustomMaps.Features;

public static class DocumentDictionary
{
    public static string Get(DocumentType type)
    {
        switch (type)
        {
            case DocumentType.Scp033:
                return "<size=15>事案012-5-033\n" +
                       "20■■年■月■日" +
                       "の定期調査にて、SCP-012がSCP-033と思われる力に侵食されてしまっていることが判明した。\n" +
                       "以前までは何事も無かったのに、急激にマゼンタ色を発し始め\n" +
                       "周囲にSCP-012影響ではなく、SCP-033影響を与えてしまっている。\n" +
                       "実に由々しき事態だ。\n" +
                       "これについて、私は本件についての大規模な調査及び対処を、強く求める。\n" +
                       "- Dr. Redheart</size>";
            case DocumentType.Scp096:
                return "<size=15>事案096-777-A\n" +
                       "20■■年■月■日、定期観察の過程で、■■■■■■■博士がSCP-096に対するセンサー監視を実施していました。\n" +
                       "観察中、SCP-096は一切の予兆なく激しい激昂行動を開始し、施設全域で感知可能なレベルの悲鳴を発しました。\n" +
                       "■■■博士は直ちに緊急アラームを作動させましたが、警備要員が現場に到着した時点で確認されたのは、多量の血痕のみでした。\n" +
                       "SCP-096はその場でうずくまった姿勢を維持しており、当該姿勢は既知の激昂行動時のものと一致していました。\n" +
                       "本事案は形式上は収束していますが、既知のトリガー事象が一切確認されていないにもかかわらずSCP-096が不安定化した可能性が高く、\n" +
                       "現行の収容・観察プロトコルが抜本的な見直しを要する段階に来ていると判断せざるを得ません。\n" +
                       "ついては、Site-02への■■■■■■■■システムの早急な導入を含む、SCP-096関連プロトコルの全面的な再評価および是正措置について、\n" +
                       "本書をもって強く要請すると同時に、これ以上の対応遅延が重大な人的・情報的損失を招くおそれがあることを、ここに重ねて申し上げます。\n" +
                       "- Dr. ■■■, Dr. Redheart</size>";
            case DocumentType.Scp3005:
                return "<size=15>SCP-3005のSite-02への移管について\n" +
                       "以前から予定されていた通り、当Site-55■■では手に負えない程\n" +
                       "SCP-3005の反ミーム性質が強まってきてしまっています。\n" +
                       "その為、先日のO5評議会にてSite-02への移管が決定されました。\n" +
                       "収容コンテナの輸送は20■■年5月5日に完了する予定です。よろしくお願します。\n" +
                       "- Site-55■■ Facility Manager</size>";
            case DocumentType.Backrooms:
                return "<b><size=55>何故あなたはここにいる？</size></b>";
            case DocumentType.Cafeteria:
                return "<size=15><b>補充申請書</b>\n" +
                       "申請者：エージェント・ストーン\n" +
                       "申請日：████/█/█\n" +
                       "品目\n" +
                       "- コーヒー豆　5袋\n" +
                       "- オーストリア産ヤギミルク　3ガロン\n" +
                       "- コーヒーミル　1台\n" +
                       "\n" +
                       "<b>申請は受理されました</b>\n" +
                       "- LCZ需品管理責任者 シェルドン・カッツカツ</size>";
            case DocumentType.DeltaWarhead:
                return "<size=15>DELTA WARHEADについて\n" +
                       "[このファイルの内容は全て■■評議会により削除されています]" +
                       "- Dr. Killistes Humano</size>";
            case DocumentType.OmegaWarhead:
                return "<size=17>OMEGA WARHEAD取扱説明書\n" +
                       "この度は私のOMEGA WARHEAD建造計画に賛同いただき、誠に...\n" +
                       "ええい、こんな物に前書きなどいらん。そうだろう？\n" +
                       "兎に角だな、この私の最高傑作の弾頭、<color=blue><b>OMEGA WARHEAD</b></color>を！\n" +
                       "何処の馬の骨かもわからん奴に向けて説明する私の事を思って！\n" +
                       "よーーーく読み込んでおくことだな！\n" +
                       "この弾頭はまず、エンジニアの協力が作動には必要だ。\n" +
                       "何故かって？そんなん、セキュリティ上の理由以外何がある！\n" +
                       "えーそしたら、地上のOMEGA WARHEADサイロに行ってサイロを開けてもらえ。\n" +
                       "...おっと、一番重要な奴も忘れていた。\n" +
                       "O5評議会から承認を必ず取り付けるように。じゃないと制御ボタンが開かんからな！\n" +
                       "承認があれば、弾頭の制御室でボタンを使ってようやく起動できるぞ！\n" +
                       "ま、俺以外の職員に使わせる気なんてないけどな！！！！！\n" +
                       "それじゃあ、グッドラック。\n" +
                       "- Dr. Aqurista Ω Boom</size>";
            case DocumentType.ScientistSamuels:
                return "<size=17>パスワード</size>\n<size=15>新しい安全プロトコルSP-02-Nに従い、\n" +
                       "個人アクセスコードはランダムな間隔\n" +
                       "（2～4週間ごと、または必要に応じて)\n" +
                       "で変更され、新しいセキュリティコードは\n" +
                       "セクションCの個人ロッカーに封された封筒で届けられます。\n" +
                       "氏名：クリストファー・サミュエルズ博士\n" +
                       "新しい個人アクセスコード：\n" +
                       "<b>1979</b>\n" +
                       "封が破られている、または封筒が何らかの形で改竄していることに気づいた場合は、\n" +
                       "<b>直ちにセキュリティ担当者にご連絡ください。</b>新しいコードをお送りします。\n" +
                       "- 警備主任 スーザン・リプリー: \n" +
                       "2137-1111-1121\n" +
                       "- 当直警備員: \n" +
                       "2137-1124-1211</size>";
            case DocumentType.ScientistMasoi:
                return "<size=17>パスワード</size>\n<size=15>新しい安全プロトコルSP-02-Nに従い、\n" +
                       "個人アクセスコードはランダムな間隔\n" +
                       "（2～4週間ごと、または必要に応じて)\n" +
                       "で変更され、新しいセキュリティコードは\n" +
                       "セクションCの個人ロッカーに封された封筒で届けられます。\n" +
                       "氏名：枡追博士\n" +
                       "新しい個人アクセスコード：\n" +
                       "<b>1217</b>\n" +
                       "封印が破られている、または封筒が何らかの形で改竄していることに気づいた場合は、\n" +
                       "<b>直ちにセキュリティ担当者にご連絡ください。</b>新しいコードをお送りします。\n" +
                       "- 警備主任 ポール・オブライエン: \n" +
                       "2137-1354-0924\n" +
                       "- 当直警備員: \n" +
                       "2137-1124-1211</size>";
            case DocumentType.ScientistGalia:
                return "<size=17>パスワード</size>\n<size=15>新しい安全プロトコルSP-02-Nに従い、\n" +
                       "個人アクセスコードはランダムな間隔\n" +
                       "（2～4週間ごと、または必要に応じて)\n" +
                       "で変更され、新しいセキュリティコードは\n" +
                       "セクションCの個人ロッカーに封された封筒で届けられます。\n" +
                       "氏名：ブルー・R・ガリア博士\n" +
                       "新しい個人アクセスコード：\n" +
                       "<b>1236</b>\n" +
                       "封印が破られている、または封筒が何らかの形で改竄していることに気づいた場合は、\n" +
                       "<b>直ちにセキュリティ担当者にご連絡ください。</b>新しいコードをお送りします。\n" +
                       "- 警備主任 ポール・オブライエン: \n" +
                       "2137-1354-0924\n" +
                       "- 当直警備員: \n" +
                       "2137-1124-1211</size>";
            case DocumentType.AboutSergey:
                return "<size=17>セルゲイ・マカロフ施設管理官について\n" +
                       "[このファイルの内容は全て倫理委員会の要請により削除されています]" +
                       "- [要請により非公開], The Ethics Comittee, Dr. Redheart</size>";
            case DocumentType.AntiAntiMeme:
                return "<size=20>Project | Anti: Anti-Meme\n" +
                       "Project Leader | Dr. Maynard\n" +
                       "Senior Researcher | Dr. Clef\n" +
                       "Researcher | Dr. Killistes Humano\n" +
                       "Researcher | Dr. Redheart</size>";
            case DocumentType.AprilWtf:
                return "REDACTED";
            case DocumentType.Overbeyond:
                return "<size=22>シュバルツシルト・クエィサァー</size>\n" +
                       "<size=20>シュバルツシルト・クエィサァー、■■■■■■■■■■の一つでありプロトコル■■■■■の副産物だ。\n" +
                       "内部の重力装置を使い量子トンネル効果を引き起こし疑似ブラックホールを生み出す事が出来る。\n" +
                       "その引力と破壊は例えば、Project Segmentumに引けを取らない戦闘力を発揮するだろう。\n" +
                       "…だが謎なのは安全性と技術だ。SQは常に熱を帯びているしなんらかの異常存在が使われているかもしれない。\n- Dr. </size>";
            case DocumentType.AboutSQ:
                return "<size=24>If you want to over the beyond, try this.</size>\n" +
                       "<size=30><b>0 7 2 7</b></size>\n\n\n\n<size=8><color=black><b>BE CURIOUS</b></color></size>";
            case DocumentType.Kai:
                return "<size=23>Kai was fucked up by Tentacle :trollface:</size>";
            case DocumentType.OperationDoc:
                return "<size=22>■■/■■\n" +
                       "作戦概要:\n" +
                       "本作戦は、Site-02のアイテムSCPオブジェクトの回収と、作戦の支障になる場合は、破壊を許可する。\n" +
                       "外部部隊は、潜入部隊と合流せよ。\n\n" +
                       "この文書は呼んだ後に必ず焼却し、廃棄せよ。\n\n" +
                       "カオス・インサージェンシー ガンマ地域司令部構成員</size>";
            case DocumentType.SpecDoc:
                return "<size=22>■■/■■\n" +
                       "特別作戦指揮者:シャン\n" +
                       "[実施対象:MF361小隊]\n" +
                       "[作戦概要]\n" +
                       "MF361小隊は、実働部隊、アルマ連隊の部隊に加わり、以下の作戦を実施せよ。\n" +
                       "作戦実施者に生命の危険が発生した場合、その他作戦隊員を放棄してよし。\n\n" +
                       "[最優先事項]\n" +
                       "- SCP-1509,1853確保 -\n" +
                       "- 要注意人物の排除 -\n" +
                       "- C.A.S.S.I.Eの指令上書き -\n\n" +
                       "[特記事項]\n" +
                       "C.A.S.S.I.Eに上書きする特別指令361の解除は、シャン小隊長デバイスのみ利用可能。\n" +
                       "その他全て考慮する必要なし。</size>";
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}

public enum DocumentType
{
    Scp033,
    Scp096,
    Scp3005,
    Backrooms,
    Cafeteria,
    DeltaWarhead,
    OmegaWarhead,
    ScientistSamuels,
    ScientistMasoi,
    ScientistGalia,
    ScientistJlolldo,
    AboutSergey,
    AntiAntiMeme,
    AprilWtf,
    Overbeyond,
    AboutSQ,
    Kai,
    OperationDoc,
    SpecDoc,
}
