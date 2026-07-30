namespace Slafight_Plugin_EXILED.API.Enums;

/// <summary>
/// 各銃器の <c>AudioModule._registeredClips</c> に登録されている AudioClip 名。
/// <c>CItemWeapon.OverrideSounds</c> のキーとしてそのまま書ける。
/// </summary>
/// <remarks>
/// <para>
/// enum ではなく const 文字列にしてあるのは、定数の値そのものが照合に使う実名だから。
/// enum にすると「メンバー名 → 実名」の対応表を別に持つことになり、二重管理で壊れやすい。
/// </para>
/// <para>
/// 値は 2026-07-30 に <c>GunSoundTestbench</c> で採取した実機ダンプそのまま。
/// 採取手順は <c>docs/internal/gun-sound-capture-checklist.md</c> を参照。
/// <b>ゲーム更新でクリップ構成が変わる可能性があるので、鳴らない場合はまず採取し直すこと。</b>
/// </para>
/// <para>
/// コメントの数字はその銃での AudioIndex。<c>(Kind)</c> 付きのものは
/// <see cref="GunSoundKind"/> でも指定できる（そちらの方が銃に依存しないので推奨）。
/// </para>
/// <para>
/// 銃をまたいで共有されているクリップが多い（<see cref="Shared"/> 参照）。
/// クリップ名から銃は特定できないので注意。
/// </para>
/// </remarks>
public static class GunSoundClips
{
    /// <summary>複数の銃で共有されているクリップ。</summary>
    public static class Shared
    {
        /// <summary>COM15 / COM18 / Com45 / FSP9 / Crossvec / E11SR / AK / FRMG0 / Logicer の空撃ち音。</summary>
        public const string NoAmmo1 = "noammo1";

        /// <summary>Revolver / A7 / Shotgun の空撃ち音。</summary>
        public const string RevDryFire = "rev_dryfire";

        /// <summary>SCP-127 の空撃ち音。</summary>
        public const string DryFire = "DRYFIRE";

        /// <summary>COM18 / Crossvec / AK / FRMG0 のアンロード音。</summary>
        public const string UnloadBullet = "Unload Bullet";

        /// <summary>COM15 / FSP9 のアンロード音。</summary>
        public const string Com15Unload = "COM15 Unload";

        /// <summary>FSP9 の ADS 音。</summary>
        public const string AdsUp = "ADS up";

        /// <summary>AK の ADS 音。</summary>
        public const string AdsDown = "ADS down";

        /// <summary>A7 / FRMG0 の薬室装填音。</summary>
        public const string Rechamber = "Rechamber";

        /// <summary>AK / FRMG0 のドラムマガジン落下音。</summary>
        public const string DrumDrop = "DrumDrop";
    }

    /// <summary>COM15。COM18 / Com45 とクリップを共有している。</summary>
    public static class Com15
    {
        public const string DryFire = Shared.NoAmmo1;        // 0 (DryFire)
        public const string Firing = "COM15 Firing";         // 1 (Gunshot)
        public const string Suppressed = "COM15 Suppressed"; // 2 (Gunshot)
        public const string Inspect1 = "COM15 Inspect 1";    // 3
        public const string Equip = "COM15 Equip";           // 4
        public const string MagIn = "COM15 Mag In";          // 5
        public const string MagOut = "COM15 Mag Out";        // 6
        public const string Pickup = "COM15 Pickup";         // 7
        public const string SlidePull = "COM15 SlidePull";   // 8
        public const string SlideRelease = "COM15 SlideRelease"; // 9
        public const string Inspect2 = "COM15 Inspect 2";    // 10
        public const string Unload = Shared.Com15Unload;     // 11
        public const string Inspect3 = "COM15 Inspect 3";    // 12
    }

    /// <summary>COM18。発砲・機構音は COM15 と同じクリップ。</summary>
    public static class Com18
    {
        public const string DryFire = Shared.NoAmmo1;            // 0 (DryFire)
        public const string Firing = Com15.Firing;               // 1 (Gunshot)
        public const string Suppressed = Com15.Suppressed;       // 2 (Gunshot)
        public const string Equip = Com15.Equip;                 // 3
        public const string MagIn = Com15.MagIn;                 // 4
        public const string MagOut = Com15.MagOut;               // 5
        public const string SlidePull = Com15.SlidePull;         // 6
        public const string SlideRelease = Com15.SlideRelease;   // 7
        public const string Unload = Shared.UnloadBullet;        // 8
        public const string Inspect0 = Ak.Inspect0;              // 9
        public const string Inspect1 = Com15.Inspect1;           // 10
        public const string Inspect2 = Com15.Inspect2;           // 11
        public const string Inspect3 = Com15.Inspect3;           // 12
    }

    /// <summary>Com45。サプレッサー発砲音を持たない。</summary>
    public static class Com45
    {
        public const string DryFire = Shared.NoAmmo1;          // 0 (DryFire)
        public const string Firing = Com15.Firing;             // 1 (Gunshot)
        public const string Equip = Com15.Equip;               // 2
        public const string Pickup = Com15.Pickup;             // 3
        public const string MagIn = Com15.MagIn;               // 4
        public const string MagOut = Com15.MagOut;             // 5
        public const string SlidePull = Com15.SlidePull;       // 6
        public const string SlideRelease = Com15.SlideRelease; // 7
    }

    /// <summary>FSP9。</summary>
    public static class Fsp9
    {
        public const string DryFire = Shared.NoAmmo1;                    // 0 (DryFire)
        public const string Firing = "FSP9 Firing";                      // 1 (Gunshot)

        /// <summary>原文どおりのタイポ（<c>Supressed</c>、p が 1 つ）。</summary>
        public const string FiringSuppressed = "FSP9 Firing Supressed";  // 2 (Gunshot)

        public const string AdsOut = "FSP9 ADS Out";                     // 3
        public const string Equip = "FSP9 Equip Without fore or stock";  // 4
        public const string AdsIn = Shared.AdsUp;                        // 5
        public const string Inspect0 = "FSP9_Inspect 0";                 // 6
        public const string MagDrop = "FSP9 Mag Drop";                   // 7
        public const string HandlePull = "FSP9 Handle Pull";             // 8
        public const string StockExtend = "FSP9 Stock Extend";           // 9
        public const string ForegripFoldOut = "FSP9 Foregrip Fold Out";  // 10
        public const string HandleRelease = "FSP9 Handle Release";       // 11
        public const string MagIn = "FSP9 Mag In";                       // 12
        public const string Unload = Shared.Com15Unload;                 // 13
        public const string Inspect1 = "FSP9_Inspect 1";                 // 14
        public const string Inspect2 = "FSP9_Inspect 2";                 // 15
        public const string BoltRelease = "FSP9 Bolt Release";           // 16
    }

    /// <summary>Crossvec。</summary>
    public static class Crossvec
    {
        public const string DryFire = Shared.NoAmmo1;                 // 0 (DryFire)
        public const string Fire = "CrossvecFire";                    // 1 (Gunshot)
        public const string Silenced = "CrossvecSilenced";            // 2 (Gunshot)
        public const string Inspect0 = "CrossvecInspect 0";           // 3
        public const string Equip = "CrossvecEquip";                  // 4
        public const string EquipStock = "CrossvecEquipStock";        // 5
        public const string HandlePull = "CrossvecHandlePull";        // 6
        public const string HandleRelease = "CrossvecHandleRelease";  // 7
        public const string MagRemove = "CrossvecMagRemove";          // 8
        public const string MagInsert = "CrossvecMagInsert";          // 9
        public const string AdsDown = "CrossvecAdsDown";              // 10
        public const string Inspect2 = "CrossvecInspect 2";           // 11
        public const string AdsUp = "CrossvecAdsUp";                  // 12
        public const string Inspect1 = "CrossvecInspect 1";           // 13
        public const string MagRemoveSlow = "CrossvecMagRemoveSlow";  // 14
        public const string Unload = Shared.UnloadBullet;             // 15
        public const string BoltRelease = "CrossvecBoltRelease";      // 16
    }

    /// <summary>E11SR。</summary>
    public static class E11Sr
    {
        public const string DryFire = Shared.NoAmmo1;                  // 0 (DryFire)
        public const string Firing = "E11SR Firing";                   // 1 (Gunshot)
        public const string Silenced = "E11SR_Silenced";               // 2 (Gunshot)
        public const string AkInspect0 = Ak.Inspect0;                  // 3
        public const string Inspect0 = "E11SR Inspect 0";              // 4
        public const string MagOut = "E11SR Mag Out";                  // 5
        public const string HandleHalfPull = "E11SR Handle HalfPull";  // 6
        public const string CrossvecInspect2 = Crossvec.Inspect2;      // 7
        public const string Equip = "E11SR Equip";                     // 8
        public const string HandlePull = "E11SR Handle Pull";          // 9
        public const string DrumIn = "E11SR Drum In";                  // 10
        public const string MagIn = "E11SR Mag In";                    // 11
        public const string HandleRelease = "E11SR Handle Release";    // 12
        public const string DrumTap = "E11SR Drum Tap";                // 13
        public const string EmptyGun = "E11SR Empty Gun";              // 14
        public const string Inspect1 = "E11SR Inspect 1";              // 15
        public const string BoltReturn = "E11SR Bolt Return";          // 16
        public const string AdsOut = Shotgun.AdsOut;                   // 17
        public const string Inspect2 = "E11SR Inspect 2";              // 18
    }

    /// <summary>AK。マガジン系は Banana(通常) と Drum(ドラム) に分かれている。</summary>
    public static class Ak
    {
        public const string DryFire = Shared.NoAmmo1;              // 0 (DryFire)
        public const string Firing = "AK Firing";                  // 1 (Gunshot)
        public const string Suppressed = "AK Suppressed";          // 2 (Gunshot)
        public const string Pickup1 = "AK Pickup 1";               // 3
        public const string Inspect0 = "AK Inspect 0";             // 4
        public const string Inspect1 = "AK Inspect 1";             // 5
        public const string Equip = "Equip No Charge";             // 6
        public const string MagRemove = "BananaRemoval";           // 7
        public const string DrumRemove = "DrumRemove";             // 8
        public const string InspectPull = "AK Inspect Pull";       // 9
        public const string MagCharging = "BananaCharging";        // 10
        public const string Pickup2 = "AK Pickup 2";               // 11
        public const string DrumInsert = "DrumInsert";             // 12
        public const string MagEjection = "BananaEjection";        // 13
        public const string MagInsertion = "BananaInsertion";      // 14
        public const string DrumDrop = Shared.DrumDrop;            // 15
        public const string InspectRelease = "AK Inspect Release"; // 16
        public const string MagImpact = "BananaImpact";            // 17
        public const string Unload = Shared.UnloadBullet;          // 18
        public const string DrumCharging = "DrumCharging";         // 19
        public const string Inspect2 = "AK Inspect 2";             // 20
        public const string AdsDown = Shared.AdsDown;              // 21
    }

    /// <summary>A7。</summary>
    public static class A7
    {
        public const string DryFire = Shared.RevDryFire; // 0 (DryFire)
        public const string Fire = "A7Fire";             // 1 (Gunshot)
        public const string PickupFirst = "PickupFirst"; // 2
        public const string Draw = "A7Draw";             // 3
        public const string AdsExit = "A7ADSExit";       // 4
        public const string RemoveMag = "RemoveMag";     // 5
        public const string PickupClose = "PickupClose"; // 6
        public const string InsertMag = "InsertMag";     // 7
        public const string Rechamber = Shared.Rechamber; // 8
    }

    /// <summary>FRMG0。<c>AR</c> = 通常マガジン、<c>BC</c> = ドラムマガジン。名前が総じて短い。</summary>
    public static class Frmg0
    {
        public const string DryFire = Shared.NoAmmo1;      // 0 (DryFire)
        public const string Fire = "Fire";                 // 1 (Gunshot)
        public const string FireSilenced = "FireSilenced"; // 2 (Gunshot)
        public const string AdsIn = "ADS in";              // 3
        public const string InspectAr1 = "Inspect AR 1";   // 4
        public const string Equip = "Equip";               // 5
        public const string InspectBc1 = "Inspect BC 1";   // 6
        public const string RemoveAr = "Remove AR";        // 7
        public const string RemoveBc = "Remove BC";        // 8
        public const string Rechamber = Shared.Rechamber;  // 9
        public const string InsertAr = "Insert AR";        // 10
        public const string InsertBc = "Insert BC";        // 11
        public const string InspectBc3 = "Inspect BC 3";   // 12
        public const string TapAr = "Tap AR";              // 13
        public const string TapBc = "Tap BC";              // 14
        public const string InspectAr3 = "Inspect AR 3";   // 15
        public const string Unload = Shared.UnloadBullet;  // 16
        public const string InspectAr2 = "Inspect AR 2";   // 17
        public const string InspectBc2 = "Inspect BC 2";   // 18
        public const string DrumDrop = Shared.DrumDrop;    // 19
    }

    /// <summary>Logicer。サプレッサー発砲音を持たない。</summary>
    public static class Logicer
    {
        public const string DryFire = Shared.NoAmmo1;                     // 0 (DryFire)
        public const string Firing = "Log Firing";                        // 1 (Gunshot)
        public const string HandlingReloadStart = "Log Handling Reload Start"; // 2
        public const string ChargingHandle = "Log Charging Handle";       // 3
        public const string LidOpen = "Log Lid Open";                     // 4
        public const string BoxUnload = "Log Box Unload";                 // 5
        public const string BoxLoad = "Log Box Load";                     // 6
        public const string LidClose = "Log Lid Close";                   // 7
        public const string Inspect53 = "Log Inspect 53";                 // 8
        public const string Inspect15 = "Log Inspect 15";                 // 9
        public const string Inspect25 = "Log Inspect 25";                 // 10
    }

    /// <summary>Shotgun。index 0 が 2 発目、1 が 1 発目で並びが逆なので注意。</summary>
    public static class Shotgun
    {
        public const string Firing2nd = "Shotgun Firing 2nd";              // 0 (Gunshot)
        public const string Firing1st = "Shotgun Firing 1st";              // 1 (Gunshot)
        public const string DryFire = Shared.RevDryFire;                   // 2 (DryFire)
        public const string ReloadTransition = "Shotgun Reload Transition"; // 3
        public const string Reload1stShell = "Shotgun Reload 1st Shell";   // 4
        public const string Reload2ndShell = "Shotgun Reload 2nd Shell";   // 5
        public const string PumpIn = "Shotgun PumpIn";                     // 6
        public const string PumpOut = "Shotgun PumpOut";                   // 7
        public const string AdsOut = "Shotgun ADS Out";                    // 8
        public const string PocketSearching3 = "Shotgun Pocket Searching 3"; // 9
        public const string Equip2ndTime = "Shotgun Equip 2nd time";       // 10
        public const string Inspect0 = "Shotgun Inspect 0";                // 11
        public const string Inspect1 = "Shotgun Inspect 1";                // 12
        public const string Inspect2 = "Shotgun Inspect 2";                // 13
    }

    /// <summary>Revolver。index 0-5 はすべて <see cref="GunSoundKind"/> で指定できる。</summary>
    public static class Revolver
    {
        public const string DryFire = Shared.RevDryFire;                    // 0 (DryFire)
        public const string DoubleAction = "rev_double_action";             // 1 (RevolverDoubleAction)
        public const string Cock = "rev_cock";                              // 2 (RevolverCocking)
        public const string Decock = "rev_decock";                          // 3 (RevolverDecocking)
        public const string Fire = "rev_fire";                              // 4 (Gunshot)
        public const string FireBuckshot = "rev_fire_buckshot";             // 5 (Gunshot)
        public const string InspectStartNormal = "rev_inspect_startnormal"; // 6
        public const string Draw = "rev_draw";                              // 7
        public const string Roulette = "rev_roulette";                      // 8
        public const string DrawPickup = "rev_draw_pickup";                 // 9
        public const string Fancy = "rev_fancy";                            // 10
        public const string Load = "rev_load";                              // 11
        public const string ReloadRare = "rev_reload_rare";                 // 12
        public const string ReloadMarauder = "rev_reload_marauder";         // 13
        public const string Reload = "rev_reload";                          // 14
        public const string DrawRare = "rev_draw_rare";                     // 15
        public const string Unload = "rev_unload";                          // 16
        public const string InspectStartSpin = "rev_inspect_startspin";     // 17
        public const string InspectMidSud = "rev_inspect_midsud";           // 18
        public const string InspectOpenCyl = "rev_inspect_opencyl";         // 19
        public const string InspectEnd = "rev_inspect_end";                 // 20
        public const string InspectCloseCyl = "rev_inspect_closecyl";       // 21
    }

    /// <summary>SCP-127。名前が全部大文字。</summary>
    public static class Scp127
    {
        public const string DryFire = Shared.DryFire;          // 0 (DryFire)
        public const string Gunshot = "GUNSHOT BOTH LAYERS";   // 1 (Gunshot)
        public const string Generic = "GENERIC";               // 2
        public const string Inspect = "INSPECT";               // 3
        public const string Equip = "EQUIP";                   // 4
        public const string PullCharging = "PULL CHARGING";    // 5
        public const string SlapCharging = "SLAP CHARGING";    // 6
    }

    /// <summary>
    /// ParticleDisruptor。<c>action</c> はチャージ / 排莢系（NoDucking チャンネル）、
    /// <c>shot</c> が発砲音（Weapons チャンネル）で、1 発ごとに両方鳴る。
    /// </summary>
    public static class ParticleDisruptor
    {
        public const string Reload = "3x_reload";                          // 0 (Reload)
        public const string Inspect0To2 = "3x_inspect0_2";                 // 1
        public const string Pickup = "3x_pickup";                          // 2
        public const string Inspect2To6 = "3x_inspect2_6";                 // 3
        public const string Inspect6To8 = "3x_inspect6_8";                 // 4
        public const string Inspect8To12 = "3x_inspect8_12";               // 5
        public const string Draw = "3x_draw";                              // 6
        public const string SingleAction = "3x_single_action";             // 7 (DisruptorAction)
        public const string SingleActionLast = "3x_single_action_last";    // 8 (DisruptorAction)
        public const string SingleShot = "3x_single_shot";                 // 9 (Gunshot)
        public const string SingleShotLast = "3x_single_shot_last";        // 10 (Gunshot)
        public const string RapidAction = "3x_rapid_action";               // 11 (DisruptorAction)
        public const string RapidActionLast = "3x_rapid_action_last";      // 12 (DisruptorAction)
        public const string RapidShot = "3x_rapid_shot";                   // 13 (Gunshot)
        public const string RapidShotLast = "3x_rapid_shot_last";          // 14 (Gunshot)
    }
}
