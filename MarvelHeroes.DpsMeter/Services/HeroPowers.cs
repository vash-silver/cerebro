using System.Collections.Generic;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Hardcoded map <c>powerPrototypeEnumIndex → hero display name</c> used by the DPS meter as a
/// fallback hero-identification signal when the avatar's <c>NetMessageEntityCreate</c> was not
/// observed (app started mid-region, user teleported before sniffer came up, etc).
///
/// <para>
/// The power prototype enum index is transported inside every <c>NetMessagePowerResult</c> via
/// <c>TransferPrototypeEnum&lt;PowerPrototype&gt;</c> — it's the 1-based position of the power's
/// <c>PrototypeDataRefRecord</c> in the sorted list of every prototype whose C# class descends
/// from <c>PowerPrototype</c> (<c>PowerPrototype</c>, <c>MissilePowerPrototype</c>,
/// <c>MovementPowerPrototype</c>, <c>SpecializationPowerPrototype</c>,
/// <c>SummonPowerPrototype</c>), ordered ascending by <c>PrototypeId</c>.
/// </para>
///
/// <para>
/// Only player-avatar powers are included (<c>Powers/Player/&lt;Hero&gt;/…</c> subtree).  Shared
/// utility powers (<c>Powers/Environment</c>, <c>Powers/Global</c>, <c>Powers/Monsters</c>, …) are
/// intentionally excluded because they can't disambiguate who's casting.  The DPS meter reads
/// this map only when the entity-id-based lookup fails, so a miss here just means we keep
/// showing "DPS" (no hero suffix) instead of picking the wrong hero.
/// </para>
///
/// <para>
/// Regenerate via:
/// </para>
///
/// <code>
/// dotnet run --project OpenCalligraphy/src/OpenCalligraphy.AvatarEnumDumper -- \
///     "C:\MrvelHeroes\MHServerEmu\Data\Game\Calligraphy.sip" \
///     --out-powers hero-powers.generated.cs
/// </code>
///
/// <para>
/// and paste the resulting <c>Dictionary&lt;uint, string&gt;</c> body in place of <see cref="Names"/>.
/// </para>
/// </summary>
internal static class HeroPowers
{
    public static readonly IReadOnlyDictionary<uint, string> Names = new Dictionary<uint, string>
    {
        {      1u, "Thor" },  // Powers/Player/Thor/Rework/BasicMeleeThunderclapCombo.prototype
        {      2u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Talents/Talent5Grenades.prototype
        {      3u, "Storm" },  // Powers/Player/Storm/Typhoon.prototype
        {      4u, "Gambit" },  // Powers/Player/Gambit/Talents/RedSuitBuff.prototype
        {      7u, "X-23" },  // Powers/Player/X23/TripleKick2ndHit.prototype
        {     10u, "Thor" },  // Powers/Player/Thor/Rework/BasicMelee.prototype
        {     14u, "Iron Man" },  // Powers/Player/IronMan/RainOfMissilesHotspotEffect.prototype
        {     16u, "Thing" },  // Powers/Player/Thing/Rework/AuraEnduranceComboExclusive.prototype
        {     18u, "Green Goblin" },  // Powers/Player/GreenGoblin/RazorBatsBleedMissileEffect.prototype
        {     20u, "Doctor Strange" },  // Powers/Player/DoctorStrange/SummonFlamesImplosion.prototype
        {     25u, "Psylocke" },  // Powers/Player/Psylocke/Bow.prototype
        {     26u, "Carnage" },  // Powers/Player/Carnage/ReapingTime.prototype
        {     30u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/InvisibilityConditionCancel.prototype
        {     32u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Talents/Talent4WitheringAgony.prototype
        {     33u, "Iceman" },  // Powers/Player/Iceman/FrostWedgeNoMovement.prototype
        {     34u, "Thor" },  // Powers/Player/Thor/NegativeStatusImmunitySecondary.prototype
        {     35u, "Nick Fury" },  // Powers/Player/NickFury/DriveBy.prototype
        {     38u, "Magik" },  // Powers/Player/Magik/NastirhLimboPortalHotspotEffect.prototype
        {     40u, "Cable" },  // Powers/Player/Cable/PulseBoltEnduranceGain.prototype
        {     42u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/JetDash.prototype
        {     47u, "Green Goblin" },  // Powers/Player/GreenGoblin/GoblinCannonInvuln.prototype
        {     51u, "X-23" },  // Powers/Player/X23/PassiveStealth.prototype
        {     54u, "Wolverine" },  // Powers/Player/Wolverine/RunThroughHit.prototype
        {     55u, "Green Goblin" },  // Powers/Player/GreenGoblin/Talents/GliderBladeTalent.prototype
        {     58u, "Blade" },  // Powers/Player/Blade/BloodlustMaxedOLD.prototype
        {     60u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent2HealthBonus.prototype
        {     61u, "Black Panther" },  // Powers/Player/BlackPanther/AcrobaticAttackCombo.prototype
        {     62u, "Hulk" },  // Powers/Player/Hulk/DeathFromAboveComboEffect.prototype
        {     63u, "Angela" },  // Powers/Player/Angela/DFAFirstActivationComboEffect.prototype
        {     68u, "Thor" },  // Powers/Player/Thor/Rework/DeathFromAbove.prototype
        {     70u, "Venom" },  // Powers/Player/Venom/MawFromAboveHealthGain.prototype
        {     72u, "Rogue" },  // Powers/Player/Rogue/ExtremeDrainHealingCombo.prototype
        {     73u, "Loki" },  // Powers/Player/Loki/Talents/FourRealmsIceShards.prototype
        {     77u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/JeanGreyTKTossPhoenixMissileEffect.prototype
        {     80u, "Iceman" },  // Powers/Player/Iceman/HotspotBeamHotspotEffectMelee.prototype
        {     81u, "X-23" },  // Powers/Player/X23/Talents/Talent1WrathGWTicksBleedDmg.prototype
        {     84u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ImplodeExplode.prototype
        {     85u, "Nova" },  // Powers/Player/Nova/HeavyBlastMissileEffect.prototype
        {     88u, "Psylocke" },  // Powers/Player/Psylocke/KickPunchMental.prototype
        {     89u, "Venom" },  // Powers/Player/Venom/Talents/BuffAtLowHealth.prototype
        {     92u, "War Machine" },  // Powers/Player/WarMachine/ArmorDamageShieldCombo.prototype
        {     93u, "Wolverine" },  // Powers/Player/Wolverine/SignatureDashSlash3.prototype
        {     96u, "Punisher" },  // Powers/Player/Punisher/Rework/ChemicalBombSlowHotspotEffect.prototype
        {     97u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/NoUCAoEMissileEffect.prototype
        {     98u, "Iceman" },  // Powers/Player/Iceman/IceBlockSnowAuraSummon.prototype
        {    100u, "Ant-Man" },  // Powers/Player/AntMan/BioElectricBlastAspdTooltip.prototype
        {    102u, "Blade" },  // Powers/Player/Blade/UVArc2ndHit.prototype
        {    105u, "Psylocke" },  // Powers/Player/Psylocke/Traits/BarrierHiddenPassive.prototype
        {    106u, "Vision" },  // Powers/Player/Vision/EnhanceRobotBuffTaunt.prototype
        {    109u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/StaffPBAoEBleed.prototype
        {    112u, "Vision" },  // Powers/Player/Vision/ControlRobotComboBuff.prototype
        {    113u, "Cable" },  // Powers/Player/Cable/PsimitarWavesPlusOuterDamageCombo.prototype
        {    114u, "Taskmaster" },  // Powers/Player/Taskmaster/UltimateSpidermanAmazingSmash.prototype
        {    115u, "Magneto" },  // Powers/Player/Magneto/DebrisVIsualPhase1Removal.prototype
        {    116u, "Cyclops" },  // Powers/Player/Cyclops/CarryTheMomentumDoTProcBurn.prototype
        {    118u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent5SigRestoresFullHealth.prototype
        {    120u, "Iron Fist" },  // Powers/Player/IronFist/FiveStanceMasteryStackCDR.prototype
        {    122u, "Black Widow" },  // Powers/Player/BlackWidow/Traits/ChargeCostCombo150.prototype
        {    123u, "Luke Cage" },  // Powers/Player/LukeCage/TumbleKickEnd.prototype
        {    125u, "Daredevil" },  // Powers/Player/Daredevil/UltimateHotspotEffect.prototype
        {    126u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/UltimateTransformComboDeactivate.prototype
        {    127u, "Deadpool" },  // Powers/Player/Deadpool/Talents/MinibossTalent.prototype
        {    128u, "Deadpool" },  // Powers/Player/Deadpool/Rework/SuperiorHealingFactorHiddenPassive.prototype
        {    134u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Talents/Talent4PhaseThroughEnemies.prototype
        {    135u, "Magik" },  // Powers/Player/Magik/BFLDAoEPunch.prototype
        {    137u, "Cyclops" },  // Powers/Player/Cyclops/CallAngelMovementHotspotEffect.prototype
        {    138u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/RangedSquirrelAoEVisual.prototype
        {    143u, "Ghost Rider" },  // Powers/Player/GhostRider/BikeLungeBasicFireballDoTStack.prototype
        {    144u, "Punisher" },  // Powers/Player/Punisher/Rework/IgnorePainUpdateBuffEffect4.prototype
        {    147u, "Black Panther" },  // Powers/Player/BlackPanther/Talents/UnseenPredatorDamageMitigationTalent.prototype
        {    149u, "She-Hulk" },  // Powers/Player/SheHulk/AssaultComboPointGain.prototype
        {    151u, "Magik" },  // Powers/Player/Magik/SoulConeSpender.prototype
        {    152u, "Deadpool" },  // Powers/Player/Deadpool/Rework/HulkHandNapalmHotspotEffect.prototype
        {    154u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ImplosionExplosionEffect.prototype
        {    155u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceImbuedBuff.prototype
        {    156u, "Loki" },  // Powers/Player/Loki/Talents/FourRealmsFrostNova.prototype
        {    157u, "Black Bolt" },  // Powers/Player/BlackBolt/Talents/Talent4SigRemap.prototype
        {    159u, "Magneto" },  // Powers/Player/Magneto/UltimateMaelstromHotspotEffect.prototype
        {    160u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/IronFistStanceSwappingSteroid.prototype
        {    161u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/AntmanAntStampedeMissileEffect.prototype
        {    167u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/MistyKnight/MistyKnightShotMissileEffect.prototype
        {    169u, "Dr. Doom" },  // Powers/Player/DrDoom/PowerResourceHiddenPassive.prototype
        {    170u, "Iron Fist" },  // Powers/Player/IronFist/Ultimate.prototype
        {    171u, "Venom" },  // Powers/Player/Venom/SigFreakoutSymbioteDrainCombo.prototype
        {    172u, "Iceman" },  // Powers/Player/Iceman/IceBlock.prototype
        {    173u, "Thing" },  // Powers/Player/Thing/Talents/Talent4ThrownObjectBuffs.prototype
        {    174u, "Cable" },  // Powers/Player/Cable/Teleport.prototype
        {    176u, "Black Bolt" },  // Powers/Player/BlackBolt/SwoopingStrikes.prototype
        {    177u, "Ant-Man" },  // Powers/Player/AntMan/AntStampedeMissileEffect.prototype
        {    179u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickVolleyMissiles.prototype
        {    180u, "X-23" },  // Powers/Player/X23/TripleKickCooldownReset.prototype
        {    185u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/UltimateKDEffect.prototype
        {    186u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexSphereHiddenPassiveProcEffect.prototype
        {    187u, "Emma Frost" },  // Powers/Player/EmmaFrost/Traits/DiamondArmorRegen.prototype
        {    188u, "Dr. Doom" },  // Powers/Player/DrDoom/ConcussiveBlasts.prototype
        {    192u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/SignatureStudentsTalent.prototype
        {    195u, "Human Torch" },  // Powers/Player/HumanTorch/UltimateExplosion.prototype
        {    196u, "Black Widow" },  // Powers/Player/BlackWidow/MicrodronesNoBreakStealth.prototype
        {    198u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/PunchKnife.prototype
        {    199u, "Dr. Doom" },  // Powers/Player/DrDoom/DiplomaticImmunity.prototype
        {    201u, "Beast" },  // Powers/Player/Beast/MeleePBAoEExtraHit.prototype
        {    214u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/NightcrawlerValiantLeapHideMesh.prototype
        {    241u, "Ghost Rider" },  // Powers/Player/GhostRider/BasicChainsNarrow.prototype
        {    242u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DamageConeDebuffProcEffect.prototype
        {    245u, "Captain America" },  // Powers/Player/CaptainAmerica/ShieldThrowPBAOEFirstHit.prototype
        {    247u, "Magik" },  // Powers/Player/Magik/BounceStrike.prototype
        {    251u, "Juggernaut" },  // Powers/Player/Juggernaut/ShockwaveMissileEffect.prototype
        {    253u, "Elektra" },  // Powers/Player/Elektra/Talents/SilentScreamTalent.prototype
        {    255u, "Hawkeye" },  // Powers/Player/Hawkeye/ThirtyArrowSpeedLoaderMissileEffect.prototype
        {    258u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/PBAoEKnockdown.prototype
        {    261u, "Thing" },  // Powers/Player/Thing/Talents/Talent5CrashingLeapBuff.prototype
        {    264u, "Luke Cage" },  // Powers/Player/LukeCage/PummelNoFinisher2ndAttack.prototype
        {    265u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCardMeleeStage1Damage.prototype
        {    269u, "Hulk" },  // Powers/Player/Hulk/Rework/PBAoESlamImpactBase.prototype
        {    270u, "Thing" },  // Powers/Player/Thing/Rework/GuessWhatTimeItIs.prototype
        {    271u, "Nova" },  // Powers/Player/Nova/Talents/Talent5DetonationPowers.prototype
        {    272u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/WarpTurretDoTHotspotEffect.prototype
        {    274u, "Winter Soldier" },  // Powers/Player/WinterSoldier/OnKillStealthComboEffect.prototype
        {    275u, "War Machine" },  // Powers/Player/WarMachine/WarMachineArmorLifeSupportHealingCombo.prototype
        {    278u, "Punisher" },  // Powers/Player/Punisher/GrenadeRecharge.prototype
        {    279u, "Loki" },  // Powers/Player/Loki/ColdFrontFrostNovaCombo.prototype
        {    280u, "Iron Fist" },  // Powers/Player/IronFist/SnakeStanceDoT.prototype
        {    287u, "Angela" },  // Powers/Player/Angela/AxeBuffsDamageAbsorption.prototype
        {    288u, "Storm" },  // Powers/Player/Storm/MicroburstKeywordConditionCombo.prototype
        {    292u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelBombsDeployHulkbuster.prototype
        {    293u, "Carnage" },  // Powers/Player/Carnage/Talents/AxeWeaponsAxeSweep.prototype
        {    294u, "Hulk" },  // Powers/Player/Hulk/Rework/SmashFace.prototype
        {    295u, "Iceman" },  // Powers/Player/Iceman/UltimateHotspotEffect.prototype
        {    296u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SecondaryResourceResetJuggernaut.prototype
        {    297u, "Vision" },  // Powers/Player/Vision/Talents/Talent1PhasePunchDefBuff.prototype
        {    298u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedEnergyGain.prototype
        {    301u, "Black Bolt" },  // Powers/Player/BlackBolt/EnergyGainOverTime6s.prototype
        {    302u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ElectricAoEGadgetSummonMiss.prototype
        {    307u, "Magneto" },  // Powers/Player/Magneto/DebrisVIsualPhase3Removal.prototype
        {    308u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosBlastMissileEffectChaosVersion.prototype
        {    310u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MrFantasticConeRapidPunch.prototype
        {    312u, "Rogue" },  // Powers/Player/Rogue/RecallOverloadPhysical.prototype
        {    313u, "Silver Surfer" },  // Powers/Player/SilverSurfer/DamageAbsorptionShieldEnduranceCost.prototype
        {    315u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedToggleAutoAttackRecurring.prototype
        {    320u, "Beast" },  // Powers/Player/Beast/FlyingBeatdownChainPower.prototype
        {    321u, "Iceman" },  // Powers/Player/Iceman/ShowOff.prototype
        {    325u, "X-23" },  // Powers/Player/X23/CrimsonCircle.prototype
        {    327u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondSweepKickDoTVuln.prototype
        {    328u, "Deadpool" },  // Powers/Player/Deadpool/MinibossProcEffect.prototype
        {    333u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PsylockeLungeNextAttackRestore.prototype
        {    334u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/InvisibilityShieldedFistDamageBonusTrigger.prototype
        {    341u, "Thing" },  // Powers/Player/Thing/Rework/Bash.prototype
        {    342u, "Rogue" },  // Powers/Player/Rogue/UltimateSignatureBamfComboBuff.prototype
        {    346u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/PsychicHammerJeanEffectJean.prototype
        {    348u, "Venom" },  // Powers/Player/Venom/FuriousLungeStealthCombo.prototype
        {    350u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveCarnageSpiritProc.prototype
        {    352u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Teleport.prototype
        {    354u, "Luke Cage" },  // Powers/Player/LukeCage/BasicPunch.prototype
        {    357u, "Moon Knight" },  // Powers/Player/MoonKnight/SummonKhonshuStatue.prototype
        {    358u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/BouncingHexSummonMarkerProcEffect.prototype
        {    360u, "Doctor Strange" },  // Powers/Player/DoctorStrange/SummonFlamesSummonLocusCombo.prototype
        {    362u, "Rogue" },  // Powers/Player/Rogue/UltimateSwordFlurry.prototype
        {    367u, "She-Hulk" },  // Powers/Player/SheHulk/FuriousLungeEffect.prototype
        {    368u, "Green Goblin" },  // Powers/Player/GreenGoblin/ElectricBlastWeakenComboEffect.prototype
        {    372u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/SeekerOrbsRemoveCharge.prototype
        {    373u, "Iron Man" },  // Powers/Player/IronMan/ShieldOverloadExplosionUpgrade3.prototype
        {    376u, "Gambit" },  // Powers/Player/Gambit/FoldEm.prototype
        {    380u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/DamageAbsorptionShield.prototype
        {    383u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Bamf.prototype
        {    384u, "Rogue" },  // Powers/Player/Rogue/UltimateDashSlashBuffEffect.prototype
        {    387u, "Black Widow" },  // Powers/Player/BlackWidow/RollingGrenades8.prototype
        {    390u, "Luke Cage" },  // Powers/Player/LukeCage/Talents/ChainSpec.prototype
        {    392u, "Thing" },  // Powers/Player/Thing/Rework/CallSuzieHotspotDoT.prototype
        {    395u, "Ant-Man" },  // Powers/Player/AntMan/MultiStrikeChainPower.prototype
        {    396u, "Ghost Rider" },  // Powers/Player/GhostRider/Talents/Talent3DefensiveHotspot.prototype
        {    397u, "Venom" },  // Powers/Player/Venom/Talents/SymbioteDrainBuffProcEffect.prototype
        {    398u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/KineticBoltJeanSpiritRestore.prototype
        {    400u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/PsychicHammerPhoenixEffect.prototype
        {    401u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BamfFrenzyPBAoE.prototype
        {    404u, "Daredevil" },  // Powers/Player/Daredevil/UltimateSaiThrow.prototype
        {    406u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/TumbleKnockdownEffect.prototype
        {    410u, "Psylocke" },  // Powers/Player/Psylocke/PsiKatanaCone.prototype
        {    412u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/CooldownSynergyEffect.prototype
        {    414u, "Wolverine" },  // Powers/Player/Wolverine/Talents/Talent4RunThroughFuryDmg.prototype
        {    419u, "Carnage" },  // Powers/Player/Carnage/KnifeBarrage.prototype
        {    420u, "Captain America" },  // Powers/Player/CaptainAmerica/Talents/ShieldBlockCooldownSerumSpec.prototype
        {    421u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/StealthToggleOutOfCombatProc.prototype
        {    422u, "Thing" },  // Powers/Player/Thing/Traits/ClobberinTime.prototype
        {    424u, "Blade" },  // Powers/Player/Blade/JustStayDown.prototype
        {    426u, "Daredevil" },  // Powers/Player/Daredevil/ShadowStrikeMovement.prototype
        {    427u, "Rogue" },  // Powers/Player/Rogue/StolenPowerLibrarySlot1.prototype
        {    428u, "Loki" },  // Powers/Player/Loki/RefractingBurstIllusionPower.prototype
        {    429u, "Taskmaster" },  // Powers/Player/Taskmaster/SwappingPassiveEnduranceProcEffect.prototype
        {    430u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/PassiveShieldStopRegenOnHitProc.prototype
        {    431u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SheHulkLawyerUp.prototype
        {    432u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Deconstruction.prototype
        {    434u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/ImplosionPulses.prototype
        {    437u, "Wolverine" },  // Powers/Player/Wolverine/RunThrough.prototype
        {    439u, "Taskmaster" },  // Powers/Player/Taskmaster/PassiveShieldStopRegenOnHitProc.prototype
        {    442u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/ForceFieldTurretSummonHotspot.prototype
        {    443u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/SignatureCrushingLeapEnd.prototype
        {    447u, "Gambit" },  // Powers/Player/Gambit/AceOfSpadesCharges.prototype
        {    449u, "Luke Cage" },  // Powers/Player/LukeCage/SummonIronFistProc.prototype
        {    451u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent3DeathFromAboveArmorSpendCDR.prototype
        {    453u, "Black Panther" },  // Powers/Player/BlackPanther/TripleShotOuterConeDamage.prototype
        {    455u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/ArcTurretSlow.prototype
        {    457u, "Emma Frost" },  // Powers/Player/EmmaFrost/UltimateComboTempInvulnerable.prototype
        {    458u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Traits/MechanicTraitMysticism.prototype
        {    459u, "Punisher" },  // Powers/Player/Punisher/Rework/SMGSelfAudioCombo.prototype
        {    460u, "Punisher" },  // Powers/Player/Punisher/Rework/SidearmsReloadCDR.prototype
        {    464u, "Wolverine" },  // Powers/Player/Wolverine/BloodySteroidFilterPower.prototype
        {    471u, "Iceman" },  // Powers/Player/Iceman/HailBallWeakenCombo.prototype
        {    472u, "Nova" },  // Powers/Player/Nova/FuriousLungeMoveSpeedBuff.prototype
        {    475u, "Green Goblin" },  // Powers/Player/GreenGoblin/Talents/NitrousTalent.prototype
        {    476u, "Daredevil" },  // Powers/Player/Daredevil/OpenerStunCombo.prototype
        {    477u, "Luke Cage" },  // Powers/Player/LukeCage/ComboPointGainProc.prototype
        {    478u, "Daredevil" },  // Powers/Player/Daredevil/SwingingAssaultStunEffect.prototype
        {    484u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent2DefenseBuffDisabler.prototype
        {    489u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/DeathFromAbove.prototype
        {    494u, "Loki" },  // Powers/Player/Loki/SwordSlice.prototype
        {    495u, "Human Torch" },  // Powers/Player/HumanTorch/BouncingFireballsNew.prototype
        {    498u, "Elektra" },  // Powers/Player/Elektra/TeleportDashProcRemoval.prototype
        {    508u, "Hawkeye" },  // Powers/Player/Hawkeye/FreezeArrowHotspotEffect.prototype
        {    513u, "Black Panther" },  // Powers/Player/BlackPanther/SmokeScreenProc.prototype
        {    514u, "Vision" },  // Powers/Player/Vision/PhasingModeUltraDense.prototype
        {    518u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/WarpTurretDeathProc.prototype
        {    519u, "Storm" },  // Powers/Player/Storm/Maelstrom.prototype
        {    522u, "Loki" },  // Powers/Player/Loki/IllusionCounterProcEffect.prototype
        {    523u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseAoEHotspotSlowEffect.prototype
        {    524u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/WarpTurretSummonArea.prototype
        {    526u, "Gambit" },  // Powers/Player/Gambit/Traits/KineticEnergyRegenEnd.prototype
        {    531u, "Beast" },  // Powers/Player/Beast/FlyingBeatdownEnd.prototype
        {    536u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosRiftInstaKill.prototype
        {    537u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/DragonSlice.prototype
        {    538u, "Punisher" },  // Powers/Player/Punisher/Rework/MinigunHiddenPassive.prototype
        {    539u, "Loki" },  // Powers/Player/Loki/DarkBoltComboHeal.prototype
        {    541u, "Black Panther" },  // Powers/Player/BlackPanther/SnareVulnerabilityHSEffect.prototype
        {    544u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GrootRideRocketHotspotSummon.prototype
        {    546u, "Magik" },  // Powers/Player/Magik/Talents/Talent4AssassinateSoulCollection.prototype
        {    547u, "Venom" },  // Powers/Player/Venom/ReviveInvulnerablityCombo.prototype
        {    550u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/UltimateAcornMeteors.prototype
        {    551u, "Green Goblin" },  // Powers/Player/GreenGoblin/FlyingFlamethrowerHotspotEffect.prototype
        {    552u, "Nightcrawler" },  // Powers/Player/Nightcrawler/FlourishBuffMaxStackCombo.prototype
        {    553u, "Punisher" },  // Powers/Player/Punisher/Rework/ReloadMagnumArmorPiercingRPGCDR.prototype
        {    560u, "Magik" },  // Powers/Player/Magik/RemoveNastirhSummon.prototype
        {    561u, "Elektra" },  // Powers/Player/Elektra/RemoveNinjaMystic.prototype
        {    563u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/LiftAndSlamPhoenix.prototype
        {    572u, "Vision" },  // Powers/Player/Vision/Talents/Talent3SelfRepair.prototype
        {    579u, "Thing" },  // Powers/Player/Thing/Rework/FoodCartOrbGate.prototype
        {    581u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BrimstoneMeteorStrike.prototype
        {    582u, "Iron Fist" },  // Powers/Player/IronFist/Talents/Talent3ShaolinStrikeBonusDamageIncrease.prototype
        {    583u, "Magik" },  // Powers/Player/Magik/DarkPactHiddenPassive.prototype
        {    584u, "Wolverine" },  // Powers/Player/Wolverine/Traits/MechanicTrait.prototype
        {    588u, "Beast" },  // Powers/Player/Beast/BeastBamfBrosSummonKurt.prototype
        {    590u, "Daredevil" },  // Powers/Player/Daredevil/RoundhouseEnabled.prototype
        {    591u, "Wolverine" },  // Powers/Player/Wolverine/Talents/CantKeepMeDownRemoveDisabler.prototype
        {    593u, "Venom" },  // Powers/Player/Venom/Talents/BuffAtLowHealthProcEffect.prototype
        {    595u, "Daredevil" },  // Powers/Player/Daredevil/TripleStrike2ndHit.prototype
        {    598u, "Green Goblin" },  // Powers/Player/GreenGoblin/FlamethrowerHotspotSummon.prototype
        {    600u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ChanneledBeamExtraBeam.prototype
        {    602u, "Magik" },  // Powers/Player/Magik/TeleportOther.prototype
        {    604u, "Green Goblin" },  // Powers/Player/GreenGoblin/MGSelfAudioCombo.prototype
        {    605u, "Gambit" },  // Powers/Player/Gambit/RaininPainExplosion.prototype
        {    608u, "Black Bolt" },  // Powers/Player/BlackBolt/MasterBlowSignaturePunchDamage.prototype
        {    610u, "Juggernaut" },  // Powers/Player/Juggernaut/HandClapDamageComboTalented.prototype
        {    611u, "Black Bolt" },  // Powers/Player/BlackBolt/Implode.prototype
        {    612u, "Punisher" },  // Powers/Player/Punisher/Ultimate.prototype
        {    620u, "Venom" },  // Powers/Player/Venom/MeleePassiveIchorSpearProc.prototype
        {    622u, "Psylocke" },  // Powers/Player/Psylocke/LungeDecoyEffect.prototype
        {    624u, "Wolverine" },  // Powers/Player/Wolverine/InCombatFuryRegen.prototype
        {    626u, "Beast" },  // Powers/Player/Beast/Talents/Talent3CloseGapCharge.prototype
        {    627u, "Gambit" },  // Powers/Player/Gambit/BoWhirlwind.prototype
        {    629u, "Loki" },  // Powers/Player/Loki/Talents/MainSpecRanged.prototype
        {    630u, "Hulk" },  // Powers/Player/Hulk/AvalancheLeapTremors.prototype
        {    631u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/JessicaJones/KickCar.prototype
        {    635u, "Beast" },  // Powers/Player/Beast/MomentumGainMechanic.prototype
        {    638u, "Taskmaster" },  // Powers/Player/Taskmaster/HawkeyeStanceBleedProc.prototype
        {    639u, "Moon Knight" },  // Powers/Player/MoonKnight/NunchuckBulldozeHotspotDoT.prototype
        {    642u, "Captain America" },  // Powers/Player/CaptainAmerica/SoundRicochet.prototype
        {    644u, "Jean Grey" },  // Powers/Player/JeanGrey/PhoenixModeToggleRezCooldownDisplay.prototype
        {    646u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveCarnageAttributeBuffProc.prototype
        {    652u, "Venom" },  // Powers/Player/Venom/FuriousLungeHealthGainProc.prototype
        {    654u, "Daredevil" },  // Powers/Player/Daredevil/Update/DeflectRatingBonusCombo.prototype
        {    656u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/DiveBombEnd.prototype
        {    657u, "Doctor Strange" },  // Powers/Player/DoctorStrange/AstralCloneProjection.prototype
        {    658u, "Vision" },  // Powers/Player/Vision/GroundSmashDamageShieldCombo.prototype
        {    659u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerCablePulseBolt.prototype
        {    661u, "Iron Fist" },  // Powers/Player/IronFist/ChiBlastVulnerability.prototype
        {    662u, "Green Goblin" },  // Powers/Player/GreenGoblin/GoblinBlastExtraHit.prototype
        {    668u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/MegaSlapDamageCombo.prototype
        {    670u, "Gambit" },  // Powers/Player/Gambit/Traits/KineticEnergyRegen.prototype
        {    671u, "Ant-Man" },  // Powers/Player/AntMan/Talents/BouncingBulletTalent.prototype
        {    673u, "Deadpool" },  // Powers/Player/Deadpool/Talents/SmellsLikeVictoryHiddenPassive.prototype
        {    674u, "Vision" },  // Powers/Player/Vision/ScorchedEarth.prototype
        {    677u, "Nova" },  // Powers/Player/Nova/PulsarInstantKillOnAvatar.prototype
        {    680u, "Hawkeye" },  // Powers/Player/Hawkeye/Volley.prototype
        {    682u, "Psylocke" },  // Powers/Player/Psylocke/PassiveHybrid.prototype
        {    685u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/JuggernautImInvulnerable.prototype
        {    686u, "War Machine" },  // Powers/Player/WarMachine/HeatGainChainGunBulletSpray.prototype
        {    687u, "Vision" },  // Powers/Player/Vision/AtomicFootDiveHotspotEffect.prototype
        {    688u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosRiftMobEffect.prototype
        {    693u, "Emma Frost" },  // Powers/Player/EmmaFrost/BasicSpiritGain.prototype
        {    696u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelBombsDismissSquirrelsInstantRemove.prototype
        {    698u, "Cable" },  // Powers/Player/Cable/PsimitarWavesPlus.prototype
        {    703u, "Blade" },  // Powers/Player/Blade/PBAoEGlaive.prototype
        {    705u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/EmmaFrostControlMob.prototype
        {    707u, "Magneto" },  // Powers/Player/Magneto/ElectromagneticShockwave.prototype
        {    713u, "Thor" },  // Powers/Player/Thor/Rework/LightningStrike.prototype
        {    716u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/PassiveShieldRegenHiddenPassive.prototype
        {    721u, "Ghost Rider" },  // Powers/Player/GhostRider/Talents/Talent4BikePowerBonuses.prototype
        {    722u, "Juggernaut" },  // Powers/Player/Juggernaut/EarthquakeLeapTremors.prototype
        {    724u, "Taskmaster" },  // Powers/Player/Taskmaster/PassiveShieldRegenHiddenPassive.prototype
        {    725u, "Ultron" },  // Powers/Player/Ultron/CommandingShot.prototype
        {    727u, "Iceman" },  // Powers/Player/Iceman/ChanneledBeam.prototype
        {    731u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/DoctorStrangeFangNuke.prototype
        {    735u, "Magik" },  // Powers/Player/Magik/OtherworldlyNovaDemonDamageBonusCombo.prototype
        {    744u, "Iron Man" },  // Powers/Player/IronMan/SignatureDamageCombo.prototype
        {    747u, "Ant-Man" },  // Powers/Player/AntMan/RedHotsPassiveAttack.prototype
        {    748u, "Juggernaut" },  // Powers/Player/Juggernaut/MomentumPunchAtkSpdCombo.prototype
        {    750u, "Nova" },  // Powers/Player/Nova/ChanneledBeamEnhancedStackingBuff.prototype
        {    752u, "Doctor Strange" },  // Powers/Player/DoctorStrange/VishantiSealMindlessOneEyeBlastEffect.prototype
        {    753u, "Luke Cage" },  // Powers/Player/LukeCage/ElbowDropComboPointGainEffect.prototype
        {    754u, "Black Panther" },  // Powers/Player/BlackPanther/EnervationDaggersCostCombo.prototype
        {    755u, "Black Widow" },  // Powers/Player/BlackWidow/MicrodronesSecondWave.prototype
        {    757u, "Angela" },  // Powers/Player/Angela/Talents/RibbonsCooldownReductionTalent.prototype
        {    758u, "Green Goblin" },  // Powers/Player/GreenGoblin/HallucinogenicPumpkin.prototype
        {    759u, "Iron Man" },  // Powers/Player/IronMan/RapidFireHiddenPassive.prototype
        {    761u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/StealthySupport.prototype
        {    763u, "Loki" },  // Powers/Player/Loki/Talents/FourRealmsRefractingBurst.prototype
        {    765u, "Cable" },  // Powers/Player/Cable/Talents/SwiftLungeLayer.prototype
        {    767u, "Vision" },  // Powers/Player/Vision/Talents/Talent3SolarFists.prototype
        {    770u, "Psylocke" },  // Powers/Player/Psylocke/PassiveDefenseHealProcVisualSelf.prototype
        {    771u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/LethargyBonus.prototype
        {    774u, "Loki" },  // Powers/Player/Loki/UltimateSummonBlizzard.prototype
        {    776u, "Venom" },  // Powers/Player/Venom/SymbioteDrainPowerWhite3.prototype
        {    779u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MsMarvelPhotonicWaveDoT.prototype
        {    781u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/zzzDeprecated/IronFistChiBurstVisual.prototype
        {    784u, "Loki" },  // Powers/Player/Loki/SearingEmbersAttackCombo.prototype
        {    785u, "Beast" },  // Powers/Player/Beast/StompSmall.prototype
        {    786u, "Daredevil" },  // Powers/Player/Daredevil/ShadowStrikeDrop.prototype
        {    789u, "Hulk" },  // Powers/Player/Hulk/Rework/ThrowRockCombo.prototype
        {    790u, "Cable" },  // Powers/Player/Cable/GreymalkinStopBombs.prototype
        {    792u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotThumperHiddenPassiveDisabler.prototype
        {    793u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHole.prototype
        {    795u, "Deadpool" },  // Powers/Player/Deadpool/Talents/TenTonHammerTalent.prototype
        {    796u, "Taskmaster" },  // Powers/Player/Taskmaster/ThreeRoundBurst.prototype
        {    797u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateHiddenPassive.prototype
        {    801u, "Gambit" },  // Powers/Player/Gambit/JacksOrBetterAoEBuff.prototype
        {    806u, "Black Bolt" },  // Powers/Player/BlackBolt/Talents/Talent5Above30PctEnergyBoost.prototype
        {    808u, "Colossus" },  // Powers/Player/Colossus/CallNightcrawlerSummonCombo.prototype
        {    809u, "Ant-Man" },  // Powers/Player/AntMan/AntWall.prototype
        {    811u, "Thor" },  // Powers/Player/Thor/Rework/RagnarokKeywordConditionCombo.prototype
        {    812u, "Colossus" },  // Powers/Player/Colossus/MetalCharge.prototype
        {    815u, "Black Panther" },  // Powers/Player/BlackPanther/DaggerCharge.prototype
        {    819u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ElektraShadowStrikeHiddenPassi.prototype
        {    821u, "War Machine" },  // Powers/Player/WarMachine/Talents/Talent3NanoEnhancers.prototype
        {    822u, "Elektra" },  // Powers/Player/Elektra/KnifeThrowEffectBoss.prototype
        {    823u, "Daredevil" },  // Powers/Player/Daredevil/TumbleMovespeedComboEffect.prototype
        {    824u, "Punisher" },  // Powers/Player/Punisher/Rework/FlamethrowerSpender.prototype
        {    825u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Disengage.prototype
        {    830u, "Cyclops" },  // Powers/Player/Cyclops/Talents/ChargeConeChargeTalent.prototype
        {    831u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/UltimateFFSpinHotspotEffect.prototype
        {    832u, "Thor" },  // Powers/Player/Thor/ImmortalCombatProcEffectTeam.prototype
        {    833u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/OnslaughtMentalOrbSelfKill.prototype
        {    835u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/MassConfusionSummonSparkleminionCombo.prototype
        {    840u, "Iron Fist" },  // Powers/Player/IronFist/Traits/OffenseTrait.prototype
        {    843u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/MegaSlapChargeAxeHeelDropBuff.prototype
        {    844u, "Deadpool" },  // Powers/Player/Deadpool/Rework/CaltropsBleedHotspotEffect.prototype
        {    845u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/StarlordChargedEGunAirMissileEffect.prototype
        {    846u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/NovaBlastoffBackwardsConeCombo.prototype
        {    850u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SuperSkrullWhirlwindFireLaunch.prototype
        {    853u, "Ant-Man" },  // Powers/Player/AntMan/Talents/FlyingAntSwarmGrowTalent.prototype
        {    854u, "Black Panther" },  // Powers/Player/BlackPanther/PantherBomb.prototype
        {    858u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/AlterRealityCooldown.prototype
        {    861u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosHex.prototype
        {    863u, "Emma Frost" },  // Powers/Player/EmmaFrost/KneelBeforeMeHotspotTaunt.prototype
        {    864u, "Cyclops" },  // Powers/Player/Cyclops/TumbleEffect.prototype
        {    867u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHolePullerPullEffect.prototype
        {    868u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelRapidFireMissileEffect.prototype
        {    869u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BoardDashExtraBeamLeft.prototype
        {    870u, "Jean Grey" },  // Powers/Player/JeanGrey/Traits/PhoenixFormEndRemoveConditions.prototype
        {    871u, "War Machine" },  // Powers/Player/WarMachine/ChaingunFullAuto.prototype
        {    875u, "Deadpool" },  // Powers/Player/Deadpool/Rework/HeadpoolHeadbutt.prototype
        {    877u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/UnlockPotentialSelfBuff.prototype
        {    884u, "Colossus" },  // Powers/Player/Colossus/GroupTauntCooldown.prototype
        {    885u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzyHitCounterBuff.prototype
        {    890u, "Thor" },  // Powers/Player/Thor/Rework/BasicRangedMissileDmgEffect.prototype
        {    891u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/KhonshuStatueSteroidCombined.prototype
        {    892u, "Hawkeye" },  // Powers/Player/Hawkeye/VolleyVisualMissileEffect.prototype
        {    894u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SlagFireMeteor.prototype
        {    895u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AoEFear.prototype
        {    896u, "Doctor Strange" },  // Powers/Player/DoctorStrange/VishantiSeal.prototype
        {    898u, "Venom" },  // Powers/Player/Venom/FearCleanseCCImmuneCombo.prototype
        {    899u, "Green Goblin" },  // Powers/Player/GreenGoblin/SignatureSpiritRestore.prototype
        {    901u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/SignatureTKHurlInvulnerability.prototype
        {    903u, "Deadpool" },  // Powers/Player/Deadpool/Rework/LungeBleedEffect.prototype
        {    908u, "Iron Man" },  // Powers/Player/IronMan/UltimateSuitChanneledBeam.prototype
        {    920u, "Blade" },  // Powers/Player/Blade/Talents/BasicCritChanceTalent.prototype
        {    922u, "Iron Fist" },  // Powers/Player/IronFist/IronFistPunchAoECombo.prototype
        {    923u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHoleBuffVersion.prototype
        {    924u, "Cable" },  // Powers/Player/Cable/PsimitarImpale.prototype
        {    928u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent2DefenseBuffRemoveHealthTransfer.prototype
        {    929u, "Iceman" },  // Powers/Player/Iceman/Traits/MechanicTraitChillShatter.prototype
        {    931u, "Taskmaster" },  // Powers/Player/Taskmaster/SteroidHotspotCDR.prototype
        {    932u, "Luke Cage" },  // Powers/Player/LukeCage/Pummel4thAttack.prototype
        {    933u, "Venom" },  // Powers/Player/Venom/SymbioteDrainPower1.prototype
        {    938u, "Captain America" },  // Powers/Player/CaptainAmerica/FinestHourBuff.prototype
        {    940u, "Thing" },  // Powers/Player/Thing/Rework/WiseCrack.prototype
        {    941u, "Black Widow" },  // Powers/Player/BlackWidow/UltimateAgentBBulletSprayEffect.prototype
        {    942u, "Ultron" },  // Powers/Player/Ultron/SignatureSteroid.prototype
        {    943u, "Ultron" },  // Powers/Player/Ultron/GroundThrow.prototype
        {    949u, "War Machine" },  // Powers/Player/WarMachine/LifeSupportCooldownDisplay.prototype
        {    951u, "Punisher" },  // Powers/Player/Punisher/Traits/DefenseTrait.prototype
        {    952u, "Magneto" },  // Powers/Player/Magneto/Talents/NegativePositivePolarity.prototype
        {    955u, "Gambit" },  // Powers/Player/Gambit/BasicKineticCardPassiveProcEffect.prototype
        {    957u, "She-Hulk" },  // Powers/Player/SheHulk/Traits/AngerFullRemoval.prototype
        {    959u, "Dr. Doom" },  // Powers/Player/DrDoom/ChanneledBeamHotspotEffect.prototype
        {    962u, "Emma Frost" },  // Powers/Player/EmmaFrost/UltimateRobotDeathProcEffect.prototype
        {    965u, "Black Panther" },  // Powers/Player/BlackPanther/DisengagingShotCombo.prototype
        {    966u, "Colossus" },  // Powers/Player/Colossus/MetalRegenerationBuffCombo.prototype
        {    967u, "Nick Fury" },  // Powers/Player/NickFury/Talents/Talent3RocketLauncherRemap.prototype
        {    970u, "Iron Man" },  // Powers/Player/IronMan/UltimateSummonSuits.prototype
        {    973u, "Magik" },  // Powers/Player/Magik/SorcerousEruptionDamageCombo.prototype
        {    976u, "Gambit" },  // Powers/Player/Gambit/GrandSlam.prototype
        {    977u, "Magik" },  // Powers/Player/Magik/DarkPactDemonConsumedSoulProjectile.prototype
        {    978u, "Ghost Rider" },  // Powers/Player/GhostRider/RideBikeHotspotsEnd.prototype
        {    980u, "Elektra" },  // Powers/Player/Elektra/KnifeRopeChain.prototype
        {    981u, "Vision" },  // Powers/Player/Vision/SolarOverchargeHiddenPassive.prototype
        {    982u, "Blade" },  // Powers/Player/Blade/BloodlustMaxedHighRiskSpecBonus.prototype
        {    983u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelBombsDismissSquirrels.prototype
        {    987u, "Punisher" },  // Powers/Player/Punisher/Rework/TumbleEffect.prototype
        {    990u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickQuiverComboFreezeArrowSummonHotspot.prototype
        {    993u, "Hawkeye" },  // Powers/Player/Hawkeye/ThreeRoundBurst.prototype
        {    997u, "Beast" },  // Powers/Player/Beast/BeastBamfAreaHitSynergized.prototype
        {    998u, "Iceman" },  // Powers/Player/Iceman/ChillFreezeCounter.prototype
        {    999u, "Captain America" },  // Powers/Player/CaptainAmerica/FinestHour.prototype
        {   1003u, "Thor" },  // Powers/Player/Thor/Talents/KnockoutSuperStrike.prototype
        {   1006u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ForceWall.prototype
        {   1009u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Talents/Talent5TargetAcquired.prototype
        {   1011u, "Wolverine" },  // Powers/Player/Wolverine/Talents/Talent3DunkBleedDmg.prototype
        {   1014u, "Beast" },  // Powers/Player/Beast/BeastDashChargeCounter.prototype
        {   1016u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotInfernoCallIn.prototype
        {   1019u, "Carnage" },  // Powers/Player/Carnage/OrganicWebbingConfusionHotspotEffect.prototype
        {   1020u, "Carnage" },  // Powers/Player/Carnage/Talents/ExcessProtectionStorage.prototype
        {   1021u, "Beast" },  // Powers/Player/Beast/Pummel.prototype
        {   1022u, "Nova" },  // Powers/Player/Nova/DFASecondaryResourceFill.prototype
        {   1024u, "Blade" },  // Powers/Player/Blade/BloodlustMaxedHighRiskStackingEffect.prototype
        {   1025u, "Beast" },  // Powers/Player/Beast/ElectroGadget.prototype
        {   1026u, "Black Bolt" },  // Powers/Player/BlackBolt/EnergyMaxxedSteroidBuff.prototype
        {   1027u, "Wolverine" },  // Powers/Player/Wolverine/UltimateBuffComboEffect.prototype
        {   1029u, "Carnage" },  // Powers/Player/Carnage/AxeDFAEnd.prototype
        {   1030u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent3GroundStompFissureLayers.prototype
        {   1031u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/ChaosPowerCostModifierCondition.prototype
        {   1039u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Traits/OffenseTrait.prototype
        {   1042u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AoEFearDoTCombo.prototype
        {   1043u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/GunTurret.prototype
        {   1044u, "Ultron" },  // Powers/Player/Ultron/ConcussionBlastBigVulnCombo.prototype
        {   1045u, "War Machine" },  // Powers/Player/WarMachine/ChaingunBurstMissileEffectAntiTank.prototype
        {   1046u, "Hawkeye" },  // Powers/Player/Hawkeye/BasicArrowExtraShot.prototype
        {   1048u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedFireBreathHotspotSlowEffect.prototype
        {   1050u, "Human Torch" },  // Powers/Player/HumanTorch/FlameTornadoCycloneHotspotPassive.prototype
        {   1052u, "Ultron" },  // Powers/Player/Ultron/Talents/Talent1MoreFlyingExplodeyDrones.prototype
        {   1058u, "Ultron" },  // Powers/Player/Ultron/FlamethrowerHotspotEffect.prototype
        {   1060u, "Gambit" },  // Powers/Player/Gambit/CardPickupFinalExplosion.prototype
        {   1062u, "Cable" },  // Powers/Player/Cable/PsychicHaze.prototype
        {   1065u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ExpandingPBAoEEffect.prototype
        {   1067u, "Black Widow" },  // Powers/Player/BlackWidow/RollingGrenades4.prototype
        {   1068u, "Iron Man" },  // Powers/Player/IronMan/SpeedRush.prototype
        {   1069u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/NeuralNetworkBonus.prototype
        {   1071u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickQuiverRandomizer.prototype
        {   1073u, "Ghost Rider" },  // Powers/Player/GhostRider/BasicFireballMissileEffect.prototype
        {   1074u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseToggleConditionRemovalB.prototype
        {   1076u, "Moon Knight" },  // Powers/Player/MoonKnight/RemoveTribute.prototype
        {   1081u, "X-23" },  // Powers/Player/X23/Pummel.prototype
        {   1082u, "Doctor Strange" },  // Powers/Player/DoctorStrange/AstralCloneProjectionLegion.prototype
        {   1084u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/RangedSquirrelAoE.prototype
        {   1088u, "Vision" },  // Powers/Player/Vision/Talents/Talent2DenseModeBuff.prototype
        {   1092u, "Human Torch" },  // Powers/Player/HumanTorch/Traits/OffenseTrait.prototype
        {   1097u, "Blade" },  // Powers/Player/Blade/DeathFromAbove.prototype
        {   1099u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/BlackKnightsGuardTalent.prototype
        {   1100u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent2BarristerBeatdownCooldownReduction.prototype
        {   1102u, "She-Hulk" },  // Powers/Player/SheHulk/CeaseAndDesist.prototype
        {   1108u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosRiftHotspotEffectMaxChaos.prototype
        {   1110u, "Kitty Pryde" },  // Powers/Player/KittyPryde/HeartCrush.prototype
        {   1111u, "Vision" },  // Powers/Player/Vision/HealingNanitesProcVersion.prototype
        {   1114u, "Winter Soldier" },  // Powers/Player/WinterSoldier/PistolShotEnduranceGain.prototype
        {   1115u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/MaxDiamondArmorBonus.prototype
        {   1117u, "Thor" },  // Powers/Player/Thor/BasicMeleeChainLightningMissileEffect.prototype
        {   1120u, "Wolverine" },  // Powers/Player/Wolverine/BasicRoninBuffDropVisual.prototype
        {   1123u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/SeekerOrbsBuffCombo.prototype
        {   1124u, "Gambit" },  // Powers/Player/Gambit/Talents/Talent1LongerBurn.prototype
        {   1126u, "Iron Man" },  // Powers/Player/IronMan/Talents/UpgradeArmorHydraulics.prototype
        {   1127u, "Hawkeye" },  // Powers/Player/Hawkeye/ExplosiveArrowMissileEffectThreeRoundBurst.prototype
        {   1129u, "Vision" },  // Powers/Player/Vision/Talents/Talent5SigDFABuff.prototype
        {   1138u, "Colossus" },  // Powers/Player/Colossus/SiberianExpressHotspotKnockback.prototype
        {   1139u, "War Machine" },  // Powers/Player/WarMachine/AlphaStrikeTempInvulnCombo.prototype
        {   1140u, "Moon Knight" },  // Powers/Player/MoonKnight/StaffPBAoE.prototype
        {   1144u, "Daredevil" },  // Powers/Player/Daredevil/Update/RoundhouseKick.prototype
        {   1152u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BlackWidowTumble.prototype
        {   1154u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHoleInstagibVersion.prototype
        {   1159u, "Iron Fist" },  // Powers/Player/IronFist/ChiRemovalProc.prototype
        {   1163u, "Loki" },  // Powers/Player/Loki/SpiritsOfTheDeadPrepare.prototype
        {   1164u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/JuggernautImInvulnerableHPassive.prototype
        {   1167u, "Moon Knight" },  // Powers/Player/MoonKnight/StrafeMissile.prototype
        {   1170u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceMechanics.prototype
        {   1171u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/UltimateBubblestorm.prototype
        {   1172u, "Ultron" },  // Powers/Player/Ultron/CleanseSelfRezCooldownDisplay.prototype
        {   1173u, "Ghost Rider" },  // Powers/Player/GhostRider/SpiritofVengeanceAsCombo.prototype
        {   1179u, "Cable" },  // Powers/Player/Cable/Talents/KineticRepulsion.prototype
        {   1180u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/AngelwingStrafeDFACharges.prototype
        {   1182u, "Angela" },  // Powers/Player/Angela/SigNoMatchStart.prototype
        {   1185u, "Doctor Strange" },  // Powers/Player/DoctorStrange/IcyTendrilsIceGolemSummon.prototype
        {   1187u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/AmpControlledMobMentalOverload.prototype
        {   1190u, "Juggernaut" },  // Powers/Player/Juggernaut/ClotheslinePunchBuffProcEffect.prototype
        {   1191u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent2DeflectBonus.prototype
        {   1198u, "Iron Fist" },  // Powers/Player/IronFist/BlackBlackPoisonTouchHealing.prototype
        {   1200u, "Beast" },  // Powers/Player/Beast/SleepGasWeakenHSEffect.prototype
        {   1202u, "Black Widow" },  // Powers/Player/BlackWidow/SweepingKickVulnerabilityCombo.prototype
        {   1205u, "Hawkeye" },  // Powers/Player/Hawkeye/Talents/PymArrowheadsTalent.prototype
        {   1207u, "Human Torch" },  // Powers/Player/HumanTorch/PassiveBurningImmunity.prototype
        {   1208u, "Ant-Man" },  // Powers/Player/AntMan/AntStampede.prototype
        {   1209u, "Angela" },  // Powers/Player/Angela/HybridTreeModSword.prototype
        {   1210u, "Carnage" },  // Powers/Player/Carnage/GroundSmash.prototype
        {   1211u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/VenomIchorSpearMissileEffect.prototype
        {   1214u, "Colossus" },  // Powers/Player/Colossus/MagikSummon/MagikDefaultAttackCombo.prototype
        {   1217u, "Black Cat" },  // Powers/Player/BlackCat/Talents/TalentTrapsShareCharges.prototype
        {   1220u, "Storm" },  // Powers/Player/Storm/DisengagingShot.prototype
        {   1222u, "Magik" },  // Powers/Player/Magik/LifeTapDoTAppliedByOtherPowerCombo.prototype
        {   1223u, "Psylocke" },  // Powers/Player/Psylocke/KatanaLeapSlashAoEEnd.prototype
        {   1225u, "War Machine" },  // Powers/Player/WarMachine/ExtendMaxHeatCondition.prototype
        {   1226u, "Thing" },  // Powers/Player/Thing/Rework/GuessWhatTimeItIsResourceCombo.prototype
        {   1227u, "Dr. Doom" },  // Powers/Player/DrDoom/FingerLasers.prototype
        {   1228u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondWhirlwindHotspotIntervalEffect.prototype
        {   1230u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SilverSurferChanneledBeamPhase3StartRefresh.prototype
        {   1231u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/ForMonkeyJoe.prototype
        {   1232u, "Iron Man" },  // Powers/Player/IronMan/BoltSprayHotspotEffect.prototype
        {   1236u, "Hulk" },  // Powers/Player/Hulk/ChargeEndEffect.prototype
        {   1239u, "Magik" },  // Powers/Player/Magik/BFLDBackhandLeft.prototype
        {   1241u, "Storm" },  // Powers/Player/Storm/LightningTempestBoltEffect.prototype
        {   1242u, "Magik" },  // Powers/Player/Magik/Traits/OffenseTrait.prototype
        {   1246u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshuSteroidSpiritVisual2.prototype
        {   1247u, "Loki" },  // Powers/Player/Loki/SorcerousBlastMissileEffect.prototype
        {   1250u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CosmicGiftDexterity.prototype
        {   1253u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SunspotPunch.prototype
        {   1254u, "Cable" },  // Powers/Player/Cable/FutureBombEnergyExplosionGunKeyword.prototype
        {   1255u, "Storm" },  // Powers/Player/Storm/ZephyrLightning.prototype
        {   1257u, "Carnage" },  // Powers/Player/Carnage/MegaClawClawPummelCDR.prototype
        {   1259u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MercWithaMouth.prototype
        {   1261u, "Black Panther" },  // Powers/Player/BlackPanther/Talents/FocEnervatingDaggersSpenderTalent.prototype
        {   1262u, "Cyclops" },  // Powers/Player/Cyclops/CarryTheMomentumDoTProcBleed.prototype
        {   1263u, "Rogue" },  // Powers/Player/Rogue/Haymaker.prototype
        {   1264u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveAntManGrowProc.prototype
        {   1265u, "Iceman" },  // Powers/Player/Iceman/IceGolemSnowballLeft.prototype
        {   1268u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/DefaultAttack2.prototype
        {   1269u, "Captain America" },  // Powers/Player/CaptainAmerica/PassiveVibraniumShieldEnduranceGain.prototype
        {   1270u, "Nova" },  // Powers/Player/Nova/Talents/Talent1MeleeFreeNovaPulse.prototype
        {   1273u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/JessicaJones/JessicaJonesDefaultAttack4.prototype
        {   1275u, "Ultron" },  // Powers/Player/Ultron/RadiationBlast.prototype
        {   1276u, "Thor" },  // Powers/Player/Thor/Rework/ThunderSpotAreaVuln.prototype
        {   1277u, "Black Widow" },  // Powers/Player/BlackWidow/RapidShot.prototype
        {   1279u, "Daredevil" },  // Powers/Player/Daredevil/Update/CritDamageTalentCombo.prototype
        {   1280u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/DoctorStrangeFangNukeBuff.prototype
        {   1281u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/ColleenWing/ColleenWingDeathFromAbove.prototype
        {   1282u, "Iron Man" },  // Powers/Player/IronMan/OrbitalBombardment.prototype
        {   1283u, "Elektra" },  // Powers/Player/Elektra/Traits/MechanicTrait.prototype
        {   1285u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/SignatureMicroNullifierMobEffect.prototype
        {   1286u, "Black Bolt" },  // Powers/Player/BlackBolt/DashProcEffect.prototype
        {   1288u, "Loki" },  // Powers/Player/Loki/DarkBolt.prototype
        {   1289u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/SpecialForcesSquirrels.prototype
        {   1292u, "Daredevil" },  // Powers/Player/Daredevil/Update/BrutalStrike.prototype
        {   1294u, "Cable" },  // Powers/Player/Cable/Talents/KineticRepulsionSummon.prototype
        {   1295u, "Gambit" },  // Powers/Player/Gambit/CheatDeathExplosion.prototype
        {   1296u, "Blade" },  // Powers/Player/Blade/HandCannonMissileEffect.prototype
        {   1297u, "Daredevil" },  // Powers/Player/Daredevil/Update/ConeYankWeakenCombo.prototype
        {   1299u, "Storm" },  // Powers/Player/Storm/HailstormHotspotEffect.prototype
        {   1300u, "Magneto" },  // Powers/Player/Magneto/ShrapnelHotspot.prototype
        {   1301u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerLadyDeadpoolMallet.prototype
        {   1302u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosRiftHotspotEffect.prototype
        {   1303u, "Blade" },  // Powers/Player/Blade/Talents/ToxinPowerFearTalent.prototype
        {   1304u, "Hulk" },  // Powers/Player/Hulk/Rework/LeapQuakeTremorsHotspotEffect.prototype
        {   1305u, "Hulk" },  // Powers/Player/Hulk/Rework/PBAoESlamImpactVeryAngry.prototype
        {   1307u, "Magik" },  // Powers/Player/Magik/DarkAllianceSummonCombo.prototype
        {   1309u, "Psylocke" },  // Powers/Player/Psylocke/DashBackstab.prototype
        {   1310u, "Dr. Doom" },  // Powers/Player/DrDoom/MagicLance.prototype
        {   1313u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/SuffocateStunCombo.prototype
        {   1315u, "Iron Man" },  // Powers/Player/IronMan/JetThrustPunch.prototype
        {   1316u, "Emma Frost" },  // Powers/Player/EmmaFrost/UltimateTRexBite.prototype
        {   1321u, "Thing" },  // Powers/Player/Thing/CallHotheadPassive.prototype
        {   1323u, "Cyclops" },  // Powers/Player/Cyclops/OpticBeamsMissileEffect.prototype
        {   1324u, "Taskmaster" },  // Powers/Player/Taskmaster/StudentsPMCDemolitionPlantCharge.prototype
        {   1325u, "Angela" },  // Powers/Player/Angela/SpartaKickEnduranceGain.prototype
        {   1327u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/TelepathyActiveDiamondVersion.prototype
        {   1328u, "Dr. Doom" },  // Powers/Player/DrDoom/DoomsdayMissilesMissileEffect.prototype
        {   1330u, "X-23" },  // Powers/Player/X23/Pummel5.prototype
        {   1333u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/UnmakeRealityMobEffect.prototype
        {   1334u, "Vision" },  // Powers/Player/Vision/Traits/OffenseTrait.prototype
        {   1336u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHoleInstagibMobEffect.prototype
        {   1337u, "Green Goblin" },  // Powers/Player/GreenGoblin/DashProcEffect.prototype
        {   1338u, "Magneto" },  // Powers/Player/Magneto/UltimateExplosion3MarkerSummon.prototype
        {   1340u, "Black Widow" },  // Powers/Player/BlackWidow/Traits/OffenseTrait.prototype
        {   1343u, "Taskmaster" },  // Powers/Player/Taskmaster/BasicShot.prototype
        {   1346u, "Elektra" },  // Powers/Player/Elektra/Talents/ProjectileMastery.prototype
        {   1350u, "Black Cat" },  // Powers/Player/BlackCat/GarrotteSlow.prototype
        {   1351u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/StealthToggleHealProcEffect.prototype
        {   1356u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/DefenseAuraDamage.prototype
        {   1358u, "Luke Cage" },  // Powers/Player/LukeCage/TumbleKickTalentStreetKickBuff.prototype
        {   1362u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GrootLifespanEndTrigger.prototype
        {   1364u, "Elektra" },  // Powers/Player/Elektra/AllCooldownResetProc.prototype
        {   1365u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/ShieldBoost.prototype
        {   1370u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallAngelEffect2.prototype
        {   1371u, "Loki" },  // Powers/Player/Loki/MagicChainsMissileEffect.prototype
        {   1375u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Assassinate.prototype
        {   1376u, "Daredevil" },  // Powers/Player/Daredevil/Update/BouncingStrike.prototype
        {   1378u, "Thor" },  // Powers/Player/Thor/Talents/SmashySmashyTalent.prototype
        {   1384u, "Magik" },  // Powers/Player/Magik/LimboSpitterDefaultAttackMissileEffect.prototype
        {   1389u, "Doctor Strange" },  // Powers/Player/DoctorStrange/AstralForm.prototype
        {   1392u, "War Machine" },  // Powers/Player/WarMachine/Talents/Talent4CoolantSystems.prototype
        {   1393u, "Human Torch" },  // Powers/Player/HumanTorch/UltimateStart.prototype
        {   1395u, "Black Panther" },  // Powers/Player/BlackPanther/TripleShotMissileEffect.prototype
        {   1396u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHoleHotspotSlowEffect.prototype
        {   1400u, "Thor" },  // Powers/Player/Thor/Talents/OdinforceSpendingTalent.prototype
        {   1403u, "Deadpool" },  // Powers/Player/Deadpool/CleverGirlDollOnDeathTrigger.prototype
        {   1408u, "Rogue" },  // Powers/Player/Rogue/UltimateSeekerButterfliesMEffect.prototype
        {   1409u, "Thing" },  // Powers/Player/Thing/Rework/CrashingLeap.prototype
        {   1411u, "Ant-Man" },  // Powers/Player/AntMan/InsectDecoy.prototype
        {   1415u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Traits/OffenseTrait.prototype
        {   1416u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerCablePlasmaBarrageHotspotEffect.prototype
        {   1422u, "Emma Frost" },  // Powers/Player/EmmaFrost/ControlMob.prototype
        {   1424u, "Black Bolt" },  // Powers/Player/BlackBolt/BasicPunchAtkSpdBuff.prototype
        {   1427u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Singularity.prototype
        {   1428u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Pancake.prototype
        {   1430u, "Thor" },  // Powers/Player/Thor/Rework/Ragnarok.prototype
        {   1431u, "Nightcrawler" },  // Powers/Player/Nightcrawler/SwordThrowEnd.prototype
        {   1435u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/GiantGunGadget.prototype
        {   1437u, "Angela" },  // Powers/Player/Angela/HevensWrathGainOverTime.prototype
        {   1438u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TelepathicIllusionPhoenix.prototype
        {   1439u, "Iron Man" },  // Powers/Player/IronMan/BasicRepulsorBeam.prototype
        {   1442u, "Ant-Man" },  // Powers/Player/AntMan/AntWallDoTCombo.prototype
        {   1445u, "Juggernaut" },  // Powers/Player/Juggernaut/ClotheslinePunch.prototype
        {   1446u, "Iceman" },  // Powers/Player/Iceman/DeepFreezeFilterPower.prototype
        {   1447u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateBattleVanMortarExplosion.prototype
        {   1448u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/RicochetCharges.prototype
        {   1449u, "Green Goblin" },  // Powers/Player/GreenGoblin/FlyingFlamethrowerCDHotspotEffect.prototype
        {   1452u, "Daredevil" },  // Powers/Player/Daredevil/ComboPointConsume.prototype
        {   1453u, "Venom" },  // Powers/Player/Venom/WebSplatIchorGain.prototype
        {   1456u, "Green Goblin" },  // Powers/Player/GreenGoblin/FlyingFlamethrowerCD.prototype
        {   1457u, "Angela" },  // Powers/Player/Angela/SwordPummel4thAttack.prototype
        {   1459u, "Beast" },  // Powers/Player/Beast/GlueBombHotspotImmobilizeEffect.prototype
        {   1463u, "Daredevil" },  // Powers/Player/Daredevil/WhirlingClubWeakenCombo.prototype
        {   1464u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ReconstructionAsProc.prototype
        {   1472u, "Nova" },  // Powers/Player/Nova/PulsarImplosionVulnerabilityComb.prototype
        {   1473u, "Nova" },  // Powers/Player/Nova/PulsarHotspotEffect.prototype
        {   1476u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AmpControlledMobHotspotEffect.prototype
        {   1477u, "Black Widow" },  // Powers/Player/BlackWidow/UltimateSummonStrikeTeamControl.prototype
        {   1480u, "Human Torch" },  // Powers/Player/HumanTorch/PassiveBurningImmunityProcEffect.prototype
        {   1482u, "Captain America" },  // Powers/Player/CaptainAmerica/PatrioticSpeech.prototype
        {   1486u, "Kitty Pryde" },  // Powers/Player/KittyPryde/DeathFromBelowEnd.prototype
        {   1488u, "Black Panther" },  // Powers/Player/BlackPanther/EnergyTrapOnDeath.prototype
        {   1494u, "Hawkeye" },  // Powers/Player/Hawkeye/DisengagingShotFlashBombCombo.prototype
        {   1496u, "Luke Cage" },  // Powers/Player/LukeCage/ElbowDropEnd.prototype
        {   1497u, "Deadpool" },  // Powers/Player/Deadpool/Rework/BasicBleedBlockRatingMultCombo.prototype
        {   1498u, "Black Widow" },  // Powers/Player/BlackWidow/CleanseHiddenPassive.prototype
        {   1501u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ThorGodlyValorFlatSpiritGain.prototype
        {   1502u, "Ultron" },  // Powers/Player/Ultron/RapidFireMissileEffect.prototype
        {   1503u, "Punisher" },  // Powers/Player/Punisher/Rework/SidearmsMissileEffect.prototype
        {   1508u, "Cable" },  // Powers/Player/Cable/GreymalkinMoveToTarget.prototype
        {   1509u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/IronFistTechniqueCraneStanceBuff.prototype
        {   1512u, "Hawkeye" },  // Powers/Player/Hawkeye/ThreeRoundBurstMissileEffect.prototype
        {   1514u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelBombsFireHulkbuster.prototype
        {   1515u, "Taskmaster" },  // Powers/Player/Taskmaster/FreezeArrowProc.prototype
        {   1517u, "Iron Man" },  // Powers/Player/IronMan/UltimateSuitRepulsorBeam2.prototype
        {   1518u, "Cable" },  // Powers/Player/Cable/PsychicHazeSlowCombo.prototype
        {   1520u, "Nick Fury" },  // Powers/Player/NickFury/Talents/Talent2RedwingRemap.prototype
        {   1521u, "Black Widow" },  // Powers/Player/BlackWidow/PlastiqueNoBreakStealth.prototype
        {   1523u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/ChaosPowerCostModifierClear.prototype
        {   1528u, "Elektra" },  // Powers/Player/Elektra/BlowDartMissileEffect.prototype
        {   1531u, "Dr. Doom" },  // Powers/Player/DrDoom/ElectricBlastPvPCooldownActiveShort.prototype
        {   1532u, "Taskmaster" },  // Powers/Player/Taskmaster/DiveKick.prototype
        {   1533u, "Blade" },  // Powers/Player/Blade/Traits/OffenseTrait.prototype
        {   1535u, "Black Bolt" },  // Powers/Player/BlackBolt/Burst.prototype
        {   1536u, "Gambit" },  // Powers/Player/Gambit/RaininPainHotspotEffect.prototype
        {   1537u, "Ghost Rider" },  // Powers/Player/GhostRider/DeathFromAbove.prototype
        {   1538u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/GoForTheEyesHit5.prototype
        {   1541u, "Beast" },  // Powers/Player/Beast/Traits/MechanicTrait.prototype
        {   1542u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateBattleVanMortars.prototype
        {   1543u, "Magneto" },  // Powers/Player/Magneto/Talents/MaelstromCooldown.prototype
        {   1546u, "Nova" },  // Powers/Player/Nova/UltimateNovaCorpsSlowEffect.prototype
        {   1548u, "Moon Knight" },  // Powers/Player/MoonKnight/HighlightSteroids.prototype
        {   1550u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MagikMiniDemonLeapAttack.prototype
        {   1555u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/LiftAndSlamJean.prototype
        {   1557u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveMoonKnightDisableHealthMinProc.prototype
        {   1560u, "Black Widow" },  // Powers/Player/BlackWidow/SniperShotStealth.prototype
        {   1561u, "Black Cat" },  // Powers/Player/BlackCat/ClawPummelClawSwipesCritBonus.prototype
        {   1568u, "Loki" },  // Powers/Player/Loki/Talents/MainSpecIllusions.prototype
        {   1572u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent2OpeningStatementBonusDamage.prototype
        {   1573u, "Gambit" },  // Powers/Player/Gambit/JacksOrBetterEndRemoveStacks.prototype
        {   1578u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HumanTorchNovaBurstEffect.prototype
        {   1579u, "Green Goblin" },  // Powers/Player/GreenGoblin/DeathFromAboveV2.prototype
        {   1580u, "Black Panther" },  // Powers/Player/BlackPanther/Snare.prototype
        {   1582u, "Ghost Rider" },  // Powers/Player/GhostRider/BuffAtLowHealthProc.prototype
        {   1584u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SquirrelGirlSquirrelPets.prototype
        {   1589u, "Angela" },  // Powers/Player/Angela/BowPBAoE.prototype
        {   1591u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Traits/DefenseTrait.prototype
        {   1595u, "Green Goblin" },  // Powers/Player/GreenGoblin/HeatSeekerBats.prototype
        {   1597u, "Vision" },  // Powers/Player/Vision/Talents/Talent3AugmentedBeams.prototype
        {   1598u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/BasicRangedSquirrelEnduranceGain.prototype
        {   1601u, "Green Goblin" },  // Powers/Player/GreenGoblin/DeathFromAbove.prototype
        {   1603u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/CableKineticBarrierDoTEffect.prototype
        {   1605u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/StormHailStormLightningTempestSummon.prototype
        {   1610u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceCostComboMedium.prototype
        {   1611u, "Black Widow" },  // Powers/Player/BlackWidow/PBAoETaser.prototype
        {   1612u, "War Machine" },  // Powers/Player/WarMachine/Talents/Talent1PlasmaCannon.prototype
        {   1613u, "Black Cat" },  // Powers/Player/BlackCat/TrapSignatureTrapCombo.prototype
        {   1614u, "Nova" },  // Powers/Player/Nova/ChargedDashSummonComboNoWindup.prototype
        {   1618u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/MinigunPetDirectCombo.prototype
        {   1621u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/BlackKnightsGuardBleed.prototype
        {   1626u, "Blade" },  // Powers/Player/Blade/DFAVulnerabilityCombo.prototype
        {   1628u, "Gambit" },  // Powers/Player/Gambit/BoWhirlwindRandomCardMissileEffect.prototype
        {   1629u, "Thing" },  // Powers/Player/Thing/Rework/CrashingLeapChargeCooldownResets.prototype
        {   1632u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BamfDiveBombEnd.prototype
        {   1635u, "Iron Fist" },  // Powers/Player/IronFist/CraneStanceSingleStanceBuff.prototype
        {   1636u, "Iron Man" },  // Powers/Player/IronMan/Talents/UpgradeOrbitalInterface.prototype
        {   1637u, "Storm" },  // Powers/Player/Storm/Talents/MaelstromBuff.prototype
        {   1638u, "Gambit" },  // Powers/Player/Gambit/BasicKineticCardMissileEffectEnhanced.prototype
        {   1639u, "Magneto" },  // Powers/Player/Magneto/Talents/DebrisGeneratorBuff.prototype
        {   1641u, "Juggernaut" },  // Powers/Player/Juggernaut/TriplePunch2ndHit.prototype
        {   1644u, "Vision" },  // Powers/Player/Vision/SolarChanneledEnergyBeamVulnerabilityEffect.prototype
        {   1647u, "Nova" },  // Powers/Player/Nova/PulsarExplosionEffectRR.prototype
        {   1648u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CleanseKnockback.prototype
        {   1650u, "Vision" },  // Powers/Player/Vision/EnhanceRobotExplosion.prototype
        {   1651u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotInfernoFireballHotspotEffect.prototype
        {   1654u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateBuffEffect.prototype
        {   1662u, "Human Torch" },  // Powers/Player/HumanTorch/ConsumeForEnduranceGainFromHots.prototype
        {   1666u, "Wolverine" },  // Powers/Player/Wolverine/BerserkerBarrageIntervalHotspotEffect.prototype
        {   1667u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/GoForTheEyes.prototype
        {   1670u, "Vision" },  // Powers/Player/Vision/SolarEnergyChargingBuffRemoval.prototype
        {   1672u, "Green Goblin" },  // Powers/Player/GreenGoblin/Talents/SignatureMovementBuffTalent.prototype
        {   1673u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerLadyDeadpoolMeleeAttack1.prototype
        {   1675u, "Carnage" },  // Powers/Player/Carnage/BasicClawsBladeStaffSecondHit1.prototype
        {   1679u, "Black Cat" },  // Powers/Player/BlackCat/ConeYankPBAoE.prototype
        {   1682u, "Ghost Rider" },  // Powers/Player/GhostRider/ConeYank.prototype
        {   1684u, "Nova" },  // Powers/Player/Nova/BouncingStrikeEnd.prototype
        {   1685u, "Luke Cage" },  // Powers/Player/LukeCage/ChunkOConcrete.prototype
        {   1687u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GambitRaginCajunHiddenPassive.prototype
        {   1690u, "Nova" },  // Powers/Player/Nova/Talents/Talent2PulsarProximityBuffs.prototype
        {   1695u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ChanneledBeamSelfAudio.prototype
        {   1698u, "Carnage" },  // Powers/Player/Carnage/ExcessTalentsDecayPauseTimer.prototype
        {   1703u, "Ant-Man" },  // Powers/Player/AntMan/Talents/AntWallTalent.prototype
        {   1704u, "Wolverine" },  // Powers/Player/Wolverine/FuryOnBleedHitFilterPower.prototype
        {   1707u, "X-23" },  // Powers/Player/X23/FuriousLungeProcEffect.prototype
        {   1708u, "Colossus" },  // Powers/Player/Colossus/KittyPrydeSummon/DefaultAttack3.prototype
        {   1709u, "Moon Knight" },  // Powers/Player/MoonKnight/NunchuckBulldoze.prototype
        {   1711u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCardConditionRemoval.prototype
        {   1713u, "Iron Fist" },  // Powers/Player/IronFist/LeopardSlashAoEProcEffect.prototype
        {   1715u, "Black Bolt" },  // Powers/Player/BlackBolt/PummelCooldownReduction.prototype
        {   1717u, "Iron Fist" },  // Powers/Player/IronFist/ChiOverload.prototype
        {   1719u, "Nova" },  // Powers/Player/Nova/BouncingStrikeHideMeshInvuln.prototype
        {   1721u, "Iceman" },  // Powers/Player/Iceman/Talents/FrozenOrbSummon.prototype
        {   1722u, "Black Cat" },  // Powers/Player/BlackCat/ClawTwirl.prototype
        {   1725u, "Iceman" },  // Powers/Player/Iceman/HailBall.prototype
        {   1726u, "Venom" },  // Powers/Player/Venom/SymbioteDrainPowerRed3.prototype
        {   1729u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent1RemoveComboPoints.prototype
        {   1730u, "Wolverine" },  // Powers/Player/Wolverine/BasicRoninBuff.prototype
        {   1732u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshuStatueTerrifyHotspot.prototype
        {   1733u, "Magik" },  // Powers/Player/Magik/SoulCaptureMinionBuffProjectileShot.prototype
        {   1734u, "Nova" },  // Powers/Player/Nova/PulsarHotspot.prototype
        {   1736u, "Iron Man" },  // Powers/Player/IronMan/ChanneledEnergyBeamEffect.prototype
        {   1737u, "Green Goblin" },  // Powers/Player/GreenGoblin/ExplosivePumpkinKeywordConditionCombo.prototype
        {   1743u, "Green Goblin" },  // Powers/Player/GreenGoblin/GhostBombMissileEffectSpecMoreGhosts.prototype
        {   1745u, "Cable" },  // Powers/Player/Cable/FutureBombPsion.prototype
        {   1748u, "Beast" },  // Powers/Player/Beast/GlueBomb.prototype
        {   1750u, "Ultron" },  // Powers/Player/Ultron/UltimateAerialDronesSummon.prototype
        {   1752u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedChannelFireEnhanced.prototype
        {   1755u, "Human Torch" },  // Powers/Player/HumanTorch/BasicFireballEffect.prototype
        {   1757u, "Nightcrawler" },  // Powers/Player/Nightcrawler/SwordPummel1stAttack.prototype
        {   1758u, "War Machine" },  // Powers/Player/WarMachine/Talents/Talent1AutoGun.prototype
        {   1759u, "Blade" },  // Powers/Player/Blade/BloodlustRises.prototype
        {   1760u, "Iceman" },  // Powers/Player/Iceman/IceArmor5PctGainCombo.prototype
        {   1761u, "Punisher" },  // Powers/Player/Punisher/Rework/PassiveToughSelfRezInvulnCombo.prototype
        {   1762u, "Beast" },  // Powers/Player/Beast/MeleeBasicAttackSpeedBuff.prototype
        {   1763u, "Iron Man" },  // Powers/Player/IronMan/Talents/UpgradeJetThrusters.prototype
        {   1767u, "Ant-Man" },  // Powers/Player/AntMan/DestroyAnts.prototype
        {   1770u, "Colossus" },  // Powers/Player/Colossus/Traits/ArmorDamageAbsorbStopper.prototype
        {   1771u, "Green Goblin" },  // Powers/Player/GreenGoblin/DashDoT.prototype
        {   1773u, "Iceman" },  // Powers/Player/Iceman/SummonStanceRangedProc.prototype
        {   1774u, "Psylocke" },  // Powers/Player/Psylocke/DashBackstabDecoyPower.prototype
        {   1775u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotsHiddenPassive.prototype
        {   1776u, "Daredevil" },  // Powers/Player/Daredevil/Update/Tumble.prototype
        {   1777u, "Elektra" },  // Powers/Player/Elektra/StaffStrike.prototype
        {   1781u, "X-23" },  // Powers/Player/X23/Tumble.prototype
        {   1783u, "Black Widow" },  // Powers/Player/BlackWidow/TumbleCCImmuneCombo.prototype
        {   1785u, "Kitty Pryde" },  // Powers/Player/KittyPryde/STSSSlash.prototype
        {   1786u, "Punisher" },  // Powers/Player/Punisher/ClaymoreActivation.prototype
        {   1787u, "Taskmaster" },  // Powers/Player/Taskmaster/ShieldBash.prototype
        {   1793u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelRapidFireEndRemoveStacks.prototype
        {   1795u, "Iron Fist" },  // Powers/Player/IronFist/FlyingKickEnd.prototype
        {   1796u, "Black Panther" },  // Powers/Player/BlackPanther/SnareHotspotDamageRanged.prototype
        {   1798u, "Thing" },  // Powers/Player/Thing/Rework/KnockoutDamageShieldCombo.prototype
        {   1804u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/IronFistHealingChiSelfBuff.prototype
        {   1808u, "Black Panther" },  // Powers/Player/BlackPanther/Talents/FanofKnivesBuffTalent.prototype
        {   1810u, "Venom" },  // Powers/Player/Venom/PBAoEBlobOnDeathBurst.prototype
        {   1812u, "Cable" },  // Powers/Player/Cable/PsimitarCycloneOuterDamageCombo.prototype
        {   1817u, "Beast" },  // Powers/Player/Beast/HulkingSlamFearCombo.prototype
        {   1818u, "Hulk" },  // Powers/Player/Hulk/Traits/AngerDecayOutOfCombat.prototype
        {   1820u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveDaredevilComboPointRemove.prototype
        {   1825u, "Black Bolt" },  // Powers/Player/BlackBolt/Talents/Talent5BarrierAutoRevive.prototype
        {   1827u, "Storm" },  // Powers/Player/Storm/ZephyrLightningStunCombo.prototype
        {   1830u, "Carnage" },  // Powers/Player/Carnage/ProtectionGain20PctSpenderMacePummel.prototype
        {   1832u, "Winter Soldier" },  // Powers/Player/WinterSoldier/KnifeThrow.prototype
        {   1835u, "Thing" },  // Powers/Player/Thing/Rework/CallHotheadHotspotEffect.prototype
        {   1836u, "Loki" },  // Powers/Player/Loki/LokiIllusionMeleeAttack.prototype
        {   1837u, "Nick Fury" },  // Powers/Player/NickFury/SummonMedic.prototype
        {   1839u, "Black Cat" },  // Powers/Player/BlackCat/Talents/TalentRandomKnockdown.prototype
        {   1840u, "Beast" },  // Powers/Player/Beast/Tumble.prototype
        {   1841u, "Iron Man" },  // Powers/Player/IronMan/UltimateSuitRepulsorBeam.prototype
        {   1842u, "Iceman" },  // Powers/Player/Iceman/FrozenOrbSummon.prototype
        {   1844u, "Iceman" },  // Powers/Player/Iceman/ShowoffTaunt.prototype
        {   1847u, "Venom" },  // Powers/Player/Venom/TentacleImpale.prototype
        {   1849u, "Storm" },  // Powers/Player/Storm/DisengagingShotMissileEffect.prototype
        {   1851u, "Gambit" },  // Powers/Player/Gambit/Talents/Talent5AceOfHearts.prototype
        {   1853u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SilverSurferChanneledBeamTargetAudio.prototype
        {   1854u, "Cable" },  // Powers/Player/Cable/TechnoOrganicVirusCooldownDisplay.prototype
        {   1864u, "Iron Man" },  // Powers/Player/IronMan/Talents/UpgradeRepulsors.prototype
        {   1865u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/CraneStanceHealthOnHit.prototype
        {   1866u, "Psylocke" },  // Powers/Player/Psylocke/SeekerButterfliesEnduranceGain.prototype
        {   1867u, "Wolverine" },  // Powers/Player/Wolverine/Talents/Talent2ImpaleBrut.prototype
        {   1876u, "Black Cat" },  // Powers/Player/BlackCat/TrapRecharge.prototype
        {   1878u, "Rogue" },  // Powers/Player/Rogue/RecallOverload.prototype
        {   1879u, "Blade" },  // Powers/Player/Blade/SerumInjectionSteroidBuff.prototype
        {   1883u, "Storm" },  // Powers/Player/Storm/TyphoonHotspotEffect.prototype
        {   1884u, "Beast" },  // Powers/Player/Beast/DeathFromAbove.prototype
        {   1885u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Traits/OffenseTrait.prototype
        {   1888u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveCarnageBuffProc.prototype
        {   1889u, "Loki" },  // Powers/Player/Loki/IllusionCounterRemoval.prototype
        {   1890u, "Blade" },  // Powers/Player/Blade/AdvancedTechniqueTimer.prototype
        {   1893u, "Iceman" },  // Powers/Player/Iceman/IceBlockReviveCDisplay.prototype
        {   1895u, "Beast" },  // Powers/Player/Beast/TetherballPBAoEMomentumCombo.prototype
        {   1902u, "Magik" },  // Powers/Player/Magik/SoulConeMissileEffect.prototype
        {   1903u, "Doctor Strange" },  // Powers/Player/DoctorStrange/VishantiSealHotspotEffect.prototype
        {   1907u, "Daredevil" },  // Powers/Player/Daredevil/ShadowStrikeInnerHit.prototype
        {   1909u, "Iceman" },  // Powers/Player/Iceman/Traits/ArmorRegenPauseTrigger.prototype
        {   1910u, "Punisher" },  // Powers/Player/Punisher/Rework/BackwardsTumbleDamageCombo.prototype
        {   1911u, "Venom" },  // Powers/Player/Venom/BigPunchInstantKillPopcorn.prototype
        {   1914u, "Emma Frost" },  // Powers/Player/EmmaFrost/Traits/MechanicTraitDiamondForm.prototype
        {   1915u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LimboDemonBossTeleportHotspotEffect.prototype
        {   1916u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/BFGHotspotEffect.prototype
        {   1919u, "Thor" },  // Powers/Player/Thor/Rework/ImmortalCombatCleanse.prototype
        {   1920u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ChanneledBeamTargetAudio.prototype
        {   1921u, "Cable" },  // Powers/Player/Cable/PsychicBullets.prototype
        {   1923u, "Black Panther" },  // Powers/Player/BlackPanther/DaggerChargeEffect.prototype
        {   1924u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/PassiveCrit.prototype
        {   1926u, "Thing" },  // Powers/Player/Thing/Rework/Knockout1stAttack.prototype
        {   1928u, "Carnage" },  // Powers/Player/Carnage/Lunge.prototype
        {   1930u, "Psylocke" },  // Powers/Player/Psylocke/PsiBoltMissileEffect.prototype
        {   1932u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/WhirlwindSummonCombo.prototype
        {   1934u, "Nightcrawler" },  // Powers/Player/Nightcrawler/FlourishBuffProcEffect.prototype
        {   1935u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ImplosionGadgetEffect.prototype
        {   1940u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondKnee.prototype
        {   1942u, "Venom" },  // Powers/Player/Venom/IchorFullVisual.prototype
        {   1943u, "Ant-Man" },  // Powers/Player/AntMan/PymSuitHiddenPassive.prototype
        {   1946u, "Ultron" },  // Powers/Player/Ultron/BetaSpecDamageShield.prototype
        {   1952u, "Black Panther" },  // Powers/Player/BlackPanther/Talents/MineFieldMeleeTalent.prototype
        {   1953u, "Hulk" },  // Powers/Player/Hulk/Rework/LeapQuakeTremorsVeryAngry.prototype
        {   1958u, "Dr. Doom" },  // Powers/Player/DrDoom/MissilesTalented.prototype
        {   1963u, "Angela" },  // Powers/Player/Angela/SpartaKickBleedCombo.prototype
        {   1965u, "Iron Man" },  // Powers/Player/IronMan/WristRocket.prototype
        {   1968u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SpiderwomanVenomBlast.prototype
        {   1969u, "Psylocke" },  // Powers/Player/Psylocke/DashStealth.prototype
        {   1974u, "Black Widow" },  // Powers/Player/BlackWidow/ElectricBatons.prototype
        {   1977u, "Venom" },  // Powers/Player/Venom/SymbioteDrain.prototype
        {   1979u, "Daredevil" },  // Powers/Player/Daredevil/ClubAttackEnduranceRegen.prototype
        {   1981u, "Daredevil" },  // Powers/Player/Daredevil/Update/ClubAttack.prototype
        {   1982u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/BlackPantherFreezingDaggerMissileEffect.prototype
        {   1983u, "Black Widow" },  // Powers/Player/BlackWidow/TumbleStunEffect.prototype
        {   1985u, "Nightcrawler" },  // Powers/Player/Nightcrawler/PassiveTeleportBuffProcEffect.prototype
        {   1987u, "Nick Fury" },  // Powers/Player/NickFury/MolecularGrenade.prototype
        {   1988u, "Silver Surfer" },  // Powers/Player/SilverSurfer/UltimateInvulnCombo.prototype
        {   1989u, "Black Cat" },  // Powers/Player/BlackCat/ClawPummel2.prototype
        {   1992u, "Vision" },  // Powers/Player/Vision/SolarOvercharge.prototype
        {   1995u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallJeanObjectExplosion.prototype
        {   1998u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/ThorMjolnirMissileEffect.prototype
        {   2004u, "Human Torch" },  // Powers/Player/HumanTorch/FlameWave.prototype
        {   2005u, "Kitty Pryde" },  // Powers/Player/KittyPryde/TagTeam.prototype
        {   2007u, "Ant-Man" },  // Powers/Player/AntMan/Talents/AntDecoyTalent.prototype
        {   2008u, "Rogue" },  // Powers/Player/Rogue/Talents/SuperDrains.prototype
        {   2011u, "Captain America" },  // Powers/Player/CaptainAmerica/Talents/NoSerumSpec.prototype
        {   2019u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseDash.prototype
        {   2022u, "War Machine" },  // Powers/Player/WarMachine/HeatGainChainGunOneOff.prototype
        {   2025u, "Magneto" },  // Powers/Player/Magneto/ChanneledCone.prototype
        {   2026u, "Carnage" },  // Powers/Player/Carnage/ClawPummel.prototype
        {   2028u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedCharge.prototype
        {   2031u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveDaredevilComboPointConsume.prototype
        {   2033u, "Loki" },  // Powers/Player/Loki/InfernalBindingMissileEffect.prototype
        {   2036u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/HammerFistTalentBuff.prototype
        {   2038u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/UltimateChanneledBeamHotspotEffe.prototype
        {   2039u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/UnstoppableChargeShorter.prototype
        {   2043u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/JessicaJones/KickCarHotspotEffect.prototype
        {   2044u, "Ant-Man" },  // Powers/Player/AntMan/DisruptorBlast.prototype
        {   2046u, "Cable" },  // Powers/Player/Cable/TechnoOrganicVirusRevive.prototype
        {   2048u, "Captain America" },  // Powers/Player/CaptainAmerica/Ultimate.prototype
        {   2049u, "Magneto" },  // Powers/Player/Magneto/Talents/AutoDebrisShieldProcEffectStronger.prototype
        {   2050u, "Ghost Rider" },  // Powers/Player/GhostRider/LoopChainWhirlwindMovingHSEffect.prototype
        {   2051u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Talents/Talent1NoLockheed.prototype
        {   2059u, "Black Widow" },  // Powers/Player/BlackWidow/MicrodronesRandomPositionQuickerHit.prototype
        {   2063u, "Deadpool" },  // Powers/Player/Deadpool/Rework/CaltropsReworkHotspotEffect.prototype
        {   2064u, "Black Panther" },  // Powers/Player/BlackPanther/EnergyTrapLanding.prototype
        {   2067u, "Iceman" },  // Powers/Player/Iceman/IceWeapon.prototype
        {   2068u, "Beast" },  // Powers/Player/Beast/ElectroGadgetSummonNegateHSCombo.prototype
        {   2071u, "Iron Fist" },  // Powers/Player/IronFist/Talents/Talent3OpenerChiBurstCombo.prototype
        {   2072u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzyRangedMissileEffectTrigger.prototype
        {   2073u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/SummonSpikedBall.prototype
        {   2076u, "Thing" },  // Powers/Player/Thing/Rework/WiseCrackTauntCombo.prototype
        {   2082u, "Storm" },  // Powers/Player/Storm/StormSurgeWindKnockback.prototype
        {   2083u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveBatroc.prototype
        {   2086u, "Taskmaster" },  // Powers/Player/Taskmaster/SwordSpin.prototype
        {   2087u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/SignatureHiddenPassiveRanks.prototype
        {   2090u, "Rogue" },  // Powers/Player/Rogue/Traits/StolenPassivePowerSlot3.prototype
        {   2093u, "Gambit" },  // Powers/Player/Gambit/BoVaultEndEnhanced.prototype
        {   2094u, "Thor" },  // Powers/Player/Thor/Rework/DeathFromAboveWeaken.prototype
        {   2095u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/UnmakeRealityProcsBuff.prototype
        {   2097u, "Angela" },  // Powers/Player/Angela/UltimateHotspotEffect.prototype
        {   2100u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MrSinisterAstralProjectionEffect.prototype
        {   2103u, "Vision" },  // Powers/Player/Vision/GroundSmashDfBDamageBonusCombo.prototype
        {   2104u, "Psylocke" },  // Powers/Player/Psylocke/PassiveDefenseHealProc.prototype
        {   2105u, "Thor" },  // Powers/Player/Thor/Rework/BasicMeleeRanged.prototype
        {   2106u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelAttackSquirrelPowerEffec.prototype
        {   2107u, "Thor" },  // Powers/Player/Thor/Rework/HammerThrowNormalMissileEffect.prototype
        {   2108u, "Human Torch" },  // Powers/Player/HumanTorch/ChargeUpBlowup.prototype
        {   2110u, "Moon Knight" },  // Powers/Player/MoonKnight/Traits/DefenseTrait.prototype
        {   2113u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Talents/Talent4MinigunBuff.prototype
        {   2116u, "Ultron" },  // Powers/Player/Ultron/ConcussionBlastVulnCombo.prototype
        {   2125u, "Black Panther" },  // Powers/Player/BlackPanther/EnergyTrap.prototype
        {   2126u, "Juggernaut" },  // Powers/Player/Juggernaut/RemoveMomentumTalentBuff.prototype
        {   2128u, "Rogue" },  // Powers/Player/Rogue/ChargeDestructibleEffect.prototype
        {   2129u, "Loki" },  // Powers/Player/Loki/Talents/FourRealmSearingEmbers.prototype
        {   2130u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/PunchElectricBatons.prototype
        {   2134u, "Ghost Rider" },  // Powers/Player/GhostRider/InfernalContract.prototype
        {   2142u, "Hulk" },  // Powers/Player/Hulk/Rework/PBAoESlam.prototype
        {   2143u, "Thing" },  // Powers/Player/Thing/Rework/WiseCrackThornsProc.prototype
        {   2144u, "Storm" },  // Powers/Player/Storm/SurgeStormSurgeBuffs.prototype
        {   2148u, "Moon Knight" },  // Powers/Player/MoonKnight/BasicGauntletPunch.prototype
        {   2150u, "Thor" },  // Powers/Player/Thor/Talents/BasicMeleeThunderclapTalent.prototype
        {   2152u, "Human Torch" },  // Powers/Player/HumanTorch/BasicFireWedge.prototype
        {   2154u, "Hulk" },  // Powers/Player/Hulk/ClapCDRProc.prototype
        {   2157u, "Cyclops" },  // Powers/Player/Cyclops/Rework/ChargeCone.prototype
        {   2167u, "Elektra" },  // Powers/Player/Elektra/TripleChainSlowCombo.prototype
        {   2175u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SauronSwoopingFlamesRecurring.prototype
        {   2177u, "Luke Cage" },  // Powers/Player/LukeCage/HeroesForHireRegenerationPassive.prototype
        {   2183u, "Emma Frost" },  // Powers/Player/EmmaFrost/UltimateSpiderBlast.prototype
        {   2184u, "Blade" },  // Powers/Player/Blade/Talents/SigDamageCooldownReductionTalent.prototype
        {   2186u, "Magneto" },  // Powers/Player/Magneto/ChanneledConeHotspotEffect.prototype
        {   2187u, "Daredevil" },  // Powers/Player/Daredevil/Update/ShadowStrike.prototype
        {   2192u, "Colossus" },  // Powers/Player/Colossus/DamageMultOnCallInUse.prototype
        {   2194u, "Ghost Rider" },  // Powers/Player/GhostRider/Talents/Talent1HellfireCombustion.prototype
        {   2198u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/PlasmaCannonHotspotEffect.prototype
        {   2200u, "Kitty Pryde" },  // Powers/Player/KittyPryde/DeathFromBelowVulnCombo.prototype
        {   2202u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent3HulkSmashBonus.prototype
        {   2204u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzyFinishingExecute.prototype
        {   2208u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerSummonCableBobHeadpool.prototype
        {   2209u, "Deadpool" },  // Powers/Player/Deadpool/Rework/OmnislashHotspotEffect.prototype
        {   2212u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/DarkHexDoTEffect.prototype
        {   2213u, "Nick Fury" },  // Powers/Player/NickFury/CommandingShout.prototype
        {   2216u, "Vision" },  // Powers/Player/Vision/PhaseSolarEnergyBonus.prototype
        {   2218u, "Black Cat" },  // Powers/Player/BlackCat/Talents/TalentSignatureRemap.prototype
        {   2219u, "Storm" },  // Powers/Player/Storm/SurgeRemovalWhenEmpty.prototype
        {   2221u, "Ultron" },  // Powers/Player/Ultron/BladeDroneSlashHit.prototype
        {   2223u, "Magik" },  // Powers/Player/Magik/NastirhBasicBlast2.prototype
        {   2232u, "Venom" },  // Powers/Player/Venom/UltimateSymbioteDrainPower.prototype
        {   2233u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/CrusherHotspotEffect.prototype
        {   2242u, "Punisher" },  // Powers/Player/Punisher/Rework/GrenadeLauncherCostCombo.prototype
        {   2245u, "Winter Soldier" },  // Powers/Player/WinterSoldier/RapidFire.prototype
        {   2247u, "Angela" },  // Powers/Player/Angela/SwordPummel2ndAttack.prototype
        {   2248u, "Thing" },  // Powers/Player/Thing/UltimateDoT.prototype
        {   2249u, "Iron Fist" },  // Powers/Player/IronFist/ChiMasteryHealthOnHitProc.prototype
        {   2250u, "Deadpool" },  // Powers/Player/Deadpool/Rework/LungeKnockdownEffect.prototype
        {   2251u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/BouncingBubbleChainEffect.prototype
        {   2255u, "Thing" },  // Powers/Player/Thing/UltimateHotspotKnockdownEffect.prototype
        {   2256u, "Ghost Rider" },  // Powers/Player/GhostRider/FirePillarVulnerabilityHSEffect.prototype
        {   2258u, "Nick Fury" },  // Powers/Player/NickFury/MolecularGrenadeRingDamageCombo.prototype
        {   2259u, "Rogue" },  // Powers/Player/Rogue/UltimateMetalRegeneration.prototype
        {   2261u, "Daredevil" },  // Powers/Player/Daredevil/Update/OpeningLungeComboEffect.prototype
        {   2264u, "Ultron" },  // Powers/Player/Ultron/Talents/Talent5RadiationBlastDefensive.prototype
        {   2268u, "Iron Man" },  // Powers/Player/IronMan/SignatureSweepRight.prototype
        {   2271u, "Black Widow" },  // Powers/Player/BlackWidow/CoupDeGrace.prototype
        {   2272u, "Black Cat" },  // Powers/Player/BlackCat/SignatureGlueTrap.prototype
        {   2277u, "Black Panther" },  // Powers/Player/BlackPanther/SweepingKick.prototype
        {   2278u, "Winter Soldier" },  // Powers/Player/WinterSoldier/ArmSmashVulnerabilityCombo.prototype
        {   2281u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHoleExplodeyVersion.prototype
        {   2282u, "Iron Fist" },  // Powers/Player/IronFist/ChiMastery.prototype
        {   2283u, "Carnage" },  // Powers/Player/Carnage/Talents/SymbioticControl.prototype
        {   2288u, "Ultron" },  // Powers/Player/Ultron/Traits/OffenseTrait.prototype
        {   2289u, "Moon Knight" },  // Powers/Player/MoonKnight/UltimateSlowEffect.prototype
        {   2291u, "Black Widow" },  // Powers/Player/BlackWidow/FlashGrenadeVulnerabilityCombo.prototype
        {   2294u, "Thor" },  // Powers/Player/Thor/Rework/LightningStrikeOFTalented.prototype
        {   2295u, "Daredevil" },  // Powers/Player/Daredevil/Update/OpeningLungeComboPointGain.prototype
        {   2296u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/UnlockPotential.prototype
        {   2297u, "Captain America" },  // Powers/Player/CaptainAmerica/ShieldThrowPassiveHiddenPassive.prototype
        {   2298u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/SignatureBouncyBallBuff.prototype
        {   2303u, "Blade" },  // Powers/Player/Blade/StakeThrower.prototype
        {   2308u, "Taskmaster" },  // Powers/Player/Taskmaster/WebsplatHotspotDamage.prototype
        {   2309u, "Jean Grey" },  // Powers/Player/JeanGrey/PhoenixForceGainProcAdditional.prototype
        {   2310u, "She-Hulk" },  // Powers/Player/SheHulk/BarristerBeatdownBlockCombo.prototype
        {   2311u, "Cable" },  // Powers/Player/Cable/FutureBombMentalExplosion.prototype
        {   2312u, "Blade" },  // Powers/Player/Blade/StakeThroughTheHeart.prototype
        {   2313u, "War Machine" },  // Powers/Player/WarMachine/AutogunMissileEffectAntiTank.prototype
        {   2315u, "Ultron" },  // Powers/Player/Ultron/SuicideDroneDeathProcEffect.prototype
        {   2317u, "Iron Man" },  // Powers/Player/IronMan/Talents/ExtremisHealingNanites.prototype
        {   2319u, "Thor" },  // Powers/Player/Thor/Rework/GroundSmashVuln.prototype
        {   2320u, "Thor" },  // Powers/Player/Thor/Talents/ForAsgardEmpoweredTalent.prototype
        {   2322u, "Loki" },  // Powers/Player/Loki/LightBeam.prototype
        {   2323u, "Loki" },  // Powers/Player/Loki/AsgardianLight.prototype
        {   2324u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CosmicWakeHotspotEffectRed.prototype
        {   2327u, "Rogue" },  // Powers/Player/Rogue/RapidPunchDashHotspotEffect.prototype
        {   2329u, "Psylocke" },  // Powers/Player/Psylocke/KatanaLeapSlashAoEDecoyEnd.prototype
        {   2330u, "Iron Fist" },  // Powers/Player/IronFist/RemoveAllIronFistTeamBuffs.prototype
        {   2331u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexSphereMissileEffectNonChaos.prototype
        {   2333u, "Moon Knight" },  // Powers/Player/MoonKnight/RicochetMissileEffect.prototype
        {   2334u, "Magik" },  // Powers/Player/Magik/BoneSpiritChargeCounter.prototype
        {   2335u, "Ant-Man" },  // Powers/Player/AntMan/Talents/STSSExtraDoT.prototype
        {   2337u, "Ant-Man" },  // Powers/Player/AntMan/BioElectricBlastHit2.prototype
        {   2339u, "Black Cat" },  // Powers/Player/BlackCat/ClawTwirlBleed.prototype
        {   2340u, "Cyclops" },  // Powers/Player/Cyclops/Talents/TeamSteroidCDResetTalent.prototype
        {   2343u, "Ghost Rider" },  // Powers/Player/GhostRider/DeathFromAboveCDR.prototype
        {   2345u, "Captain America" },  // Powers/Player/CaptainAmerica/ShieldBlockSpecProc.prototype
        {   2351u, "Green Goblin" },  // Powers/Player/GreenGoblin/TheBigOneDamageProc.prototype
        {   2352u, "Dr. Doom" },  // Powers/Player/DrDoom/ServoGuardArmy.prototype
        {   2355u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotFlyerSkillshotMissileEffect.prototype
        {   2361u, "Iron Fist" },  // Powers/Player/IronFist/Pummel.prototype
        {   2362u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/SpecialForcesSummonCombo.prototype
        {   2363u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/MistyKnight/ConcussiveBlast.prototype
        {   2364u, "Nick Fury" },  // Powers/Player/NickFury/EyesEverywhereSniperShotArea.prototype
        {   2368u, "Elektra" },  // Powers/Player/Elektra/KillCommandMasterSlash.prototype
        {   2369u, "Ant-Man" },  // Powers/Player/AntMan/Traits/OffenseTrait.prototype
        {   2371u, "Gambit" },  // Powers/Player/Gambit/RoyalFlushMissileExpireEffect.prototype
        {   2378u, "Wolverine" },  // Powers/Player/Wolverine/BerserkerBarrage.prototype
        {   2383u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/SquirrelSaboteursBonus.prototype
        {   2384u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/IronMaidenProcEffect.prototype
        {   2388u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ChanneledBeamBuffPhase3Refresh.prototype
        {   2389u, "Cable" },  // Powers/Player/Cable/TelepathicIllusionAttack.prototype
        {   2392u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateBattleVanPartingShotEff.prototype
        {   2395u, "Black Cat" },  // Powers/Player/BlackCat/UltimateHotspot.prototype
        {   2396u, "Colossus" },  // Powers/Player/Colossus/MagikSummon/MagikDefaultAttack.prototype
        {   2398u, "Deadpool" },  // Powers/Player/Deadpool/Rework/LilDeadpool.prototype
        {   2399u, "Blade" },  // Powers/Player/Blade/UVGrenadeDamageCombo.prototype
        {   2400u, "Magik" },  // Powers/Player/Magik/DarkPactDemonShieldGrant.prototype
        {   2401u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BlackWidowTumbleEnd.prototype
        {   2403u, "Ghost Rider" },  // Powers/Player/GhostRider/FireBreath.prototype
        {   2405u, "War Machine" },  // Powers/Player/WarMachine/WarMachineArmorMeleeCritProcEffect.prototype
        {   2410u, "Nova" },  // Powers/Player/Nova/FuriousLungeEffect.prototype
        {   2414u, "Captain America" },  // Powers/Player/CaptainAmerica/BoomerangThrow.prototype
        {   2417u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Talents/Talent2BrutalKiller.prototype
        {   2418u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DarkPhoenixSpecHybridPhoenixBuff.prototype
        {   2429u, "Cyclops" },  // Powers/Player/Cyclops/FocusBeamSelfAudio.prototype
        {   2430u, "Beast" },  // Powers/Player/Beast/FlyingBeatdownHelper.prototype
        {   2431u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveDrax.prototype
        {   2435u, "Rogue" },  // Powers/Player/Rogue/Talents/RecallOverloadMental.prototype
        {   2436u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/FireGiantBossExplosion.prototype
        {   2439u, "Dr. Doom" },  // Powers/Player/DrDoom/DoomBotRepulsors.prototype
        {   2440u, "Venom" },  // Powers/Player/Venom/MeleeBasicHealthGain.prototype
        {   2443u, "Elektra" },  // Powers/Player/Elektra/TeleportDashProcEffect.prototype
        {   2444u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/UltimateNoMorePrepareEndExplosion.prototype
        {   2448u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/RangedSplashShotMissileEffect.prototype
        {   2450u, "Iceman" },  // Powers/Player/Iceman/SpikePunchIcewallKiller.prototype
        {   2452u, "Green Goblin" },  // Powers/Player/GreenGoblin/Talents/GasPumpkinIgniteTalent.prototype
        {   2454u, "Deadpool" },  // Powers/Player/Deadpool/Rework/StrafeMissilePowerRandom.prototype
        {   2456u, "Black Cat" },  // Powers/Player/BlackCat/Talents/TalentInstantKillPopcorn.prototype
        {   2457u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotThumperHiddenPassive.prototype
        {   2459u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/MistyKnight/MistyKnightShockwave.prototype
        {   2463u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotInfernoHiddenPassiveDisabler.prototype
        {   2465u, "Captain America" },  // Powers/Player/CaptainAmerica/DeathFromAboveEnd.prototype
        {   2466u, "Ghost Rider" },  // Powers/Player/GhostRider/LoopChainWhirlwindMoving.prototype
        {   2470u, "Luke Cage" },  // Powers/Player/LukeCage/HighlightComboFinishers.prototype
        {   2471u, "Cable" },  // Powers/Player/Cable/TelepathicIllusion.prototype
        {   2472u, "Vision" },  // Powers/Player/Vision/Dash.prototype
        {   2473u, "Black Panther" },  // Powers/Player/BlackPanther/SmokeScreenSpecMovementBuff.prototype
        {   2475u, "Cable" },  // Powers/Player/Cable/PsimitarCycloneVulnCombo.prototype
        {   2477u, "Hawkeye" },  // Powers/Player/Hawkeye/Talents/TurretArrowCooldownTalent.prototype
        {   2480u, "Iceman" },  // Powers/Player/Iceman/Traits/DefenseTrait.prototype
        {   2481u, "War Machine" },  // Powers/Player/WarMachine/WarMachineArmorExplosion.prototype
        {   2483u, "Hulk" },  // Powers/Player/Hulk/Rework/PassiveToughRezCooldownDisplay.prototype
        {   2484u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/SignatureTKHurlJean.prototype
        {   2486u, "Human Torch" },  // Powers/Player/HumanTorch/Traits/HeatRegenProcEffect.prototype
        {   2488u, "Ultron" },  // Powers/Player/Ultron/PrimeSummonBuff.prototype
        {   2493u, "Thor" },  // Powers/Player/Thor/Rework/TauntCombo.prototype
        {   2494u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/BasicMeleeSquirrelBonus.prototype
        {   2496u, "Magik" },  // Powers/Player/Magik/BoneSpiritMissileEffect.prototype
        {   2499u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshuSteroidHealthCombo.prototype
        {   2501u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/RavenousBindingSlowEffectTransfer.prototype
        {   2502u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/RockTrollBerserkerHealthReductionProc.prototype
        {   2506u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/NastirhHealingShield.prototype
        {   2512u, "Black Panther" },  // Powers/Player/BlackPanther/FreezingDaggers.prototype
        {   2516u, "She-Hulk" },  // Powers/Player/SheHulk/BarristerBeatdown3rdHit.prototype
        {   2518u, "Emma Frost" },  // Powers/Player/EmmaFrost/Traits/DefenseTrait.prototype
        {   2519u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GrootDefaultAttackCombo.prototype
        {   2520u, "Carnage" },  // Powers/Player/Carnage/ReapingTimeCDRProc.prototype
        {   2525u, "Moon Knight" },  // Powers/Player/MoonKnight/MultiPersonalitySelfRezCDDisplay.prototype
        {   2526u, "Black Widow" },  // Powers/Player/BlackWidow/RapidBiteSelfAudioCombo.prototype
        {   2530u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissilePowerSpeed1000.prototype
        {   2533u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/UnmakeRealityBossEffect.prototype
        {   2535u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondWhirlwind.prototype
        {   2537u, "Black Bolt" },  // Powers/Player/BlackBolt/HypersonicScreamBossEffect.prototype
        {   2540u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateCallJean.prototype
        {   2541u, "Captain America" },  // Powers/Player/CaptainAmerica/BackwardsTumbleEffect.prototype
        {   2544u, "Iceman" },  // Powers/Player/Iceman/ShowoffStun.prototype
        {   2545u, "Ultron" },  // Powers/Player/Ultron/MeleeDroneTaunt.prototype
        {   2548u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedChannelFireEndCombo.prototype
        {   2549u, "Taskmaster" },  // Powers/Player/Taskmaster/StudentsMaggiaSecondGoonHomeRun.prototype
        {   2551u, "Venom" },  // Powers/Player/Venom/ConeTendrils.prototype
        {   2552u, "Black Widow" },  // Powers/Player/BlackWidow/RapidBite.prototype
        {   2553u, "Iron Fist" },  // Powers/Player/IronFist/SnakeStanceVisual.prototype
        {   2554u, "Iceman" },  // Powers/Player/Iceman/IceBlockSnowAuraHotspotEffect.prototype
        {   2556u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/KhonshuSteroidCastSpeedMult.prototype
        {   2557u, "Kitty Pryde" },  // Powers/Player/KittyPryde/DeathFromBelowSwordDamageBuff.prototype
        {   2558u, "Luke Cage" },  // Powers/Player/LukeCage/Yank.prototype
        {   2559u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/DoubleStrikeSecondHit.prototype
        {   2560u, "Ghost Rider" },  // Powers/Player/GhostRider/PassiveRegenRevivalHealing.prototype
        {   2562u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedFireBreath.prototype
        {   2565u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/RenounceCyttorak.prototype
        {   2567u, "Thor" },  // Powers/Player/Thor/Traits/MechanicTraitOdinforce.prototype
        {   2568u, "Daredevil" },  // Powers/Player/Daredevil/Update/NunchuckBulldozeComboSummon.prototype
        {   2569u, "Storm" },  // Powers/Player/Storm/LightningBoltEnduranceGain.prototype
        {   2573u, "Black Bolt" },  // Powers/Player/BlackBolt/Talents/Talent4SigRangeBoost.prototype
        {   2575u, "Thing" },  // Powers/Player/Thing/Rework/CallSuzie.prototype
        {   2579u, "Black Cat" },  // Powers/Player/BlackCat/DeathFromAbove.prototype
        {   2584u, "Moon Knight" },  // Powers/Player/MoonKnight/CrescentBolaDoTMissileEffect.prototype
        {   2585u, "War Machine" },  // Powers/Player/WarMachine/TearGasHotspotConfuseEffect.prototype
        {   2587u, "Black Widow" },  // Powers/Player/BlackWidow/RapidBiteMissileEffect.prototype
        {   2588u, "Thor" },  // Powers/Player/Thor/Rework/KnockOutDamageCombo.prototype
        {   2591u, "Thing" },  // Powers/Player/Thing/Rework/YancyStreetGangHomeRun.prototype
        {   2592u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseTeleport.prototype
        {   2594u, "Black Widow" },  // Powers/Player/BlackWidow/RollingGrenades3.prototype
        {   2597u, "Psylocke" },  // Powers/Player/Psylocke/LungeDecoyPower.prototype
        {   2598u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveRockTrollBerserker.prototype
        {   2602u, "Dr. Doom" },  // Powers/Player/DrDoom/DoomBots.prototype
        {   2604u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexSphereMissileEffect.prototype
        {   2606u, "Cable" },  // Powers/Player/Cable/ParticleAcceleratorBuffCombo.prototype
        {   2607u, "Angela" },  // Powers/Player/Angela/SwordLunge.prototype
        {   2608u, "Black Bolt" },  // Powers/Player/BlackBolt/MasterBlowSignatureAoECombo.prototype
        {   2609u, "Ultron" },  // Powers/Player/Ultron/Talents/Talent1SpinAttackCooldownReset.prototype
        {   2611u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Traits/DefenseTrait.prototype
        {   2615u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhasingPunchNextPunchBonus.prototype
        {   2616u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/SeekerOrbsProc.prototype
        {   2619u, "Angela" },  // Powers/Player/Angela/WhippingRibbonsBeam.prototype
        {   2620u, "Thing" },  // Powers/Player/Thing/Rework/FoodCart.prototype
        {   2624u, "Elektra" },  // Powers/Player/Elektra/BlowDartMissileEffectCritBonus.prototype
        {   2626u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent5ClapBonus.prototype
        {   2627u, "Black Cat" },  // Powers/Player/BlackCat/GlueTrap.prototype
        {   2629u, "Nick Fury" },  // Powers/Player/NickFury/Execute.prototype
        {   2631u, "Deadpool" },  // Powers/Player/Deadpool/SummonHealthOrb.prototype
        {   2635u, "Hulk" },  // Powers/Player/Hulk/Rework/LeapQuakeEnd.prototype
        {   2637u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateSummonXmenJean.prototype
        {   2638u, "Moon Knight" },  // Powers/Player/MoonKnight/PassiveCarbonadiumArmorUpdate.prototype
        {   2639u, "Ant-Man" },  // Powers/Player/AntMan/AnthillProc.prototype
        {   2642u, "Venom" },  // Powers/Player/Venom/PBAoEBlobHealProc.prototype
        {   2643u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/TacticsGunsDoTProc.prototype
        {   2644u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/HERBIE.prototype
        {   2647u, "Black Cat" },  // Powers/Player/BlackCat/SignatureGasTrap.prototype
        {   2648u, "Vision" },  // Powers/Player/Vision/GroundSmashEnd.prototype
        {   2652u, "Doctor Strange" },  // Powers/Player/DoctorStrange/BasicDaggersSetCountOnProjection.prototype
        {   2653u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GorgonStoneGazeStunEffect.prototype
        {   2655u, "Hawkeye" },  // Powers/Player/Hawkeye/AdamantiumArrowMissileEffect.prototype
        {   2656u, "Thing" },  // Powers/Player/Thing/Traits/ClobberinTimeGainOnHitNotTime.prototype
        {   2657u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/ForcePushJean.prototype
        {   2660u, "Beast" },  // Powers/Player/Beast/Talents/Talent1DFARemap.prototype
        {   2664u, "Magneto" },  // Powers/Player/Magneto/ShrapnelCone.prototype
        {   2667u, "Iron Fist" },  // Powers/Player/IronFist/ChiBurst.prototype
        {   2672u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/RobbieReyesDriveByHotspotMissile.prototype
        {   2673u, "Dr. Doom" },  // Powers/Player/DrDoom/MagicOrbHotspotEffect.prototype
        {   2675u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ObfuscationHotspotProtectedEffect.prototype
        {   2678u, "Colossus" },  // Powers/Player/Colossus/Ultimate.prototype
        {   2679u, "Vision" },  // Powers/Player/Vision/ModeToggleSwitchToDenseShortBuff.prototype
        {   2680u, "Cyclops" },  // Powers/Player/Cyclops/TeamSteroidBuffVisualFeedback.prototype
        {   2682u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ForcePillarAsProc.prototype
        {   2683u, "Silver Surfer" },  // Powers/Player/SilverSurfer/TimeWarpSelfRez.prototype
        {   2684u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ElectricAoEGadgetStun.prototype
        {   2687u, "Magik" },  // Powers/Player/Magik/NastirhLimboPortal.prototype
        {   2689u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateBattleVanFrontHotspot.prototype
        {   2693u, "She-Hulk" },  // Powers/Player/SheHulk/BarristerBeatdown10thHit.prototype
        {   2694u, "Loki" },  // Powers/Player/Loki/SearingEmbersPrepare.prototype
        {   2695u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/ImplosionJeanSummonMarkerCombo.prototype
        {   2696u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SuperSkrullWhirlwindFireRecurringEffect.prototype
        {   2700u, "Vision" },  // Powers/Player/Vision/SolarOverchargeCDR.prototype
        {   2701u, "Magneto" },  // Powers/Player/Magneto/Lunge.prototype
        {   2702u, "Black Widow" },  // Powers/Player/BlackWidow/ConductiveGrenade.prototype
        {   2710u, "Thing" },  // Powers/Player/Thing/InterceptingCharge.prototype
        {   2712u, "Vision" },  // Powers/Player/Vision/SigFullEnduranceGain.prototype
        {   2713u, "Nova" },  // Powers/Player/Nova/ChargedDashStunEffect.prototype
        {   2714u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/MistyKnight/MistyKnightShockwaveMissileEffec.prototype
        {   2715u, "Winter Soldier" },  // Powers/Player/WinterSoldier/BulletSpraySlowEffect.prototype
        {   2716u, "Nova" },  // Powers/Player/Nova/PulsarSpiritRestoration2.prototype
        {   2718u, "Black Bolt" },  // Powers/Player/BlackBolt/Talents/Talent2PummelReset.prototype
        {   2722u, "Storm" },  // Powers/Player/Storm/MicroburstRingDamageCombo.prototype
        {   2723u, "Daredevil" },  // Powers/Player/Daredevil/UltimateShadowStrikeReappear.prototype
        {   2725u, "Punisher" },  // Powers/Player/Punisher/Rework/IgnorePainUpdateBuffEffect3.prototype
        {   2727u, "Cable" },  // Powers/Player/Cable/Traits/OffenseTrait.prototype
        {   2730u, "Hawkeye" },  // Powers/Player/Hawkeye/ExplosiveArrow.prototype
        {   2740u, "Iceman" },  // Powers/Player/Iceman/IceGolemSummonProc.prototype
        {   2741u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LivingLaserLaserBlast.prototype
        {   2742u, "Nick Fury" },  // Powers/Player/NickFury/Traits/DefaultAmmoRegenEnd.prototype
        {   2743u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GrootOutSaplingAttack.prototype
        {   2744u, "Magneto" },  // Powers/Player/Magneto/UltimateSentinelPartThrownPower.prototype
        {   2745u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/UltimateInstaKillSummonsEffect.prototype
        {   2746u, "Iceman" },  // Powers/Player/Iceman/DeepFreezeEffectWeak.prototype
        {   2750u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent2SigPowersCDR.prototype
        {   2751u, "Ghost Rider" },  // Powers/Player/GhostRider/ContractSteroidHiddenPassive.prototype
        {   2754u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/JessicaJones/Taunt.prototype
        {   2756u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Traits/DefenseTrait.prototype
        {   2757u, "X-23" },  // Powers/Player/X23/MoveMechanicHiddenPassive.prototype
        {   2758u, "Ghost Rider" },  // Powers/Player/GhostRider/BikeLungeHitEffect.prototype
        {   2760u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexSphereOrbVisual2.prototype
        {   2761u, "Ghost Rider" },  // Powers/Player/GhostRider/Traits/MechanicTraitFlameTrail.prototype
        {   2766u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/JessicaJones/KickCarMissileEffect.prototype
        {   2767u, "Carnage" },  // Powers/Player/Carnage/BasicClawsBladeStaffWasUsedLast.prototype
        {   2770u, "Rogue" },  // Powers/Player/Rogue/UltimateBasicSlash.prototype
        {   2775u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/AcornMeteorNutcrackerBonus.prototype
        {   2776u, "Hawkeye" },  // Powers/Player/Hawkeye/DoubleShotHealCombo.prototype
        {   2777u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/UltimateTimerDisplay.prototype
        {   2778u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent4MovementBuildBuffs.prototype
        {   2779u, "Loki" },  // Powers/Player/Loki/ConeOfColdHotspotEffect.prototype
        {   2781u, "War Machine" },  // Powers/Player/WarMachine/SpecCloakingDeviceGunBuffComboEffect.prototype
        {   2786u, "Nick Fury" },  // Powers/Player/NickFury/SummonRifleman.prototype
        {   2787u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MagnetoAllInPickupRangeCombo.prototype
        {   2790u, "Gambit" },  // Powers/Player/Gambit/UltimateCardThrow7.prototype
        {   2791u, "Hulk" },  // Powers/Player/Hulk/Rework/SmashFaceBleedCombo.prototype
        {   2795u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelAttackSummonAllSquirrels.prototype
        {   2803u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Tumble.prototype
        {   2804u, "Thing" },  // Powers/Player/Thing/Rework/DiscusToss.prototype
        {   2806u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BetaRayBillLightningBarrageHSEff.prototype
        {   2807u, "Captain America" },  // Powers/Player/CaptainAmerica/Talents/StrengthenedTeamBuffSpec.prototype
        {   2808u, "Iron Fist" },  // Powers/Player/IronFist/LeopardSlashStanceBuff.prototype
        {   2809u, "Black Cat" },  // Powers/Player/BlackCat/AssassinateDamageCombo.prototype
        {   2810u, "Hulk" },  // Powers/Player/Hulk/Rework/MeteorDebrisDamageCombo.prototype
        {   2811u, "Gambit" },  // Powers/Player/Gambit/Talents/Talent4StackTheDeck.prototype
        {   2813u, "Storm" },  // Powers/Player/Storm/LightningSpecLightningColumnSummonProc.prototype
        {   2814u, "Silver Surfer" },  // Powers/Player/SilverSurfer/DeconstructionFilterPower.prototype
        {   2820u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/HammerFist.prototype
        {   2825u, "Thing" },  // Powers/Player/Thing/Rework/Headbutt.prototype
        {   2828u, "Ghost Rider" },  // Powers/Player/GhostRider/BasicChainsFilterPower.prototype
        {   2829u, "Magik" },  // Powers/Player/Magik/OtherworldlyNovaMissileEffect.prototype
        {   2836u, "Taskmaster" },  // Powers/Player/Taskmaster/RangeBuffProc.prototype
        {   2841u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GrootOutVisualEffect.prototype
        {   2846u, "Wolverine" },  // Powers/Player/Wolverine/Talents/Talent2TornadoClawCharges.prototype
        {   2847u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent3HostileWitnessBonus.prototype
        {   2852u, "Black Widow" },  // Powers/Player/BlackWidow/AcrobaticAttackProcEffect.prototype
        {   2853u, "Iceman" },  // Powers/Player/Iceman/IcicleIceWallKiller.prototype
        {   2854u, "Black Widow" },  // Powers/Player/BlackWidow/RemoteDetonatorUnmap.prototype
        {   2859u, "She-Hulk" },  // Powers/Player/SheHulk/LawyerUpBuffComboMaxStacks.prototype
        {   2862u, "X-23" },  // Powers/Player/X23/Talents/Talent1MaxWrath.prototype
        {   2867u, "Iron Fist" },  // Powers/Player/IronFist/SnakeSingleStanceBuff.prototype
        {   2868u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Talents/Talent5AoeCooldowns.prototype
        {   2871u, "Nova" },  // Powers/Player/Nova/PBAoENukeIncreaseEffectCombo.prototype
        {   2872u, "Daredevil" },  // Powers/Player/Daredevil/UltimateSummonHotspot.prototype
        {   2873u, "Black Cat" },  // Powers/Player/BlackCat/NineLivesDisableHealthMinProc.prototype
        {   2874u, "Dr. Doom" },  // Powers/Player/DrDoom/UnworthyPistolPvPCooldownActive.prototype
        {   2880u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateBattleVanAutoGun.prototype
        {   2883u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/CaptainsStrengthTalent.prototype
        {   2884u, "Taskmaster" },  // Powers/Player/Taskmaster/StudentsPMCRiflemanRifleBurstHSE.prototype
        {   2887u, "Captain America" },  // Powers/Player/CaptainAmerica/HeroicStrikeShieldSwipeCombo.prototype
        {   2888u, "Ant-Man" },  // Powers/Player/AntMan/Talents/HealthOnShrinkHitTalent.prototype
        {   2889u, "Green Goblin" },  // Powers/Player/GreenGoblin/Talents/ExplosivePumpkinInnerDamage.prototype
        {   2891u, "Green Goblin" },  // Powers/Player/GreenGoblin/GasPumpkin.prototype
        {   2892u, "Ghost Rider" },  // Powers/Player/GhostRider/ChainLineAoE.prototype
        {   2893u, "Thing" },  // Powers/Player/Thing/Rework/LampBatThrow.prototype
        {   2896u, "Green Goblin" },  // Powers/Player/GreenGoblin/BombingCircleTempInvulnCombo.prototype
        {   2902u, "Winter Soldier" },  // Powers/Player/WinterSoldier/ArmBlastMechanics.prototype
        {   2904u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BigBeamMissileCombo.prototype
        {   2906u, "War Machine" },  // Powers/Player/WarMachine/ThermalShotDefenseBuffCombo.prototype
        {   2908u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/TeamStealthBuffEffect.prototype
        {   2911u, "Psylocke" },  // Powers/Player/Psylocke/ButterflynadoHotspotEffect.prototype
        {   2914u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SilverSurferChanneledBeamPhase3Start.prototype
        {   2915u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Traits/DefenseTrait.prototype
        {   2917u, "Iron Man" },  // Powers/Player/IronMan/MissileCritPassiveProcMissileEff.prototype
        {   2918u, "Luke Cage" },  // Powers/Player/LukeCage/SummonMistyKnightCombo.prototype
        {   2920u, "Green Goblin" },  // Powers/Player/GreenGoblin/DeathFromAboveEnd.prototype
        {   2925u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Talents/Talent5BasicRifleBuff.prototype
        {   2926u, "Angela" },  // Powers/Player/Angela/HevensWrathDamageBoostBuffs.prototype
        {   2928u, "Juggernaut" },  // Powers/Player/Juggernaut/MomentumGainMechanic.prototype
        {   2929u, "Captain America" },  // Powers/Player/CaptainAmerica/Talents/ShieldStrikeBonusDamageSpec.prototype
        {   2931u, "Iron Man" },  // Powers/Player/IronMan/ShieldOverloadExplosionUpgrade4.prototype
        {   2932u, "Deadpool" },  // Powers/Player/Deadpool/Rework/Minigun.prototype
        {   2933u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ChanneledBeamPhase1Loop.prototype
        {   2940u, "Iron Man" },  // Powers/Player/IronMan/ShieldOverloadUnstableCoreProc.prototype
        {   2942u, "Black Cat" },  // Powers/Player/BlackCat/Signature.prototype
        {   2946u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/RangedSquirrelAoEAttachedHotspotPassive.prototype
        {   2947u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ObfuscationHiddenPassive.prototype
        {   2948u, "Storm" },  // Powers/Player/Storm/StormSurgeWindTempestSummonAgent.prototype
        {   2952u, "Angela" },  // Powers/Player/Angela/SwordLungeSlowCombo.prototype
        {   2953u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/SignatureCooldown.prototype
        {   2954u, "Black Bolt" },  // Powers/Player/BlackBolt/BarrierAsProc.prototype
        {   2957u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzyCCImmunity.prototype
        {   2960u, "Ultron" },  // Powers/Player/Ultron/LeapStrikeCombo.prototype
        {   2961u, "Nick Fury" },  // Powers/Player/NickFury/Traits/OffenseTrait.prototype
        {   2962u, "Taskmaster" },  // Powers/Player/Taskmaster/BasicShotTwoMissileEffect.prototype
        {   2964u, "Jean Grey" },  // Powers/Player/JeanGrey/PsiShield.prototype
        {   2968u, "Black Panther" },  // Powers/Player/BlackPanther/ClawUppercutBleedCombo.prototype
        {   2972u, "Doctor Strange" },  // Powers/Player/DoctorStrange/ProjectionFangNukeMissileEffect.prototype
        {   2973u, "Venom" },  // Powers/Player/Venom/RangedBasic.prototype
        {   2975u, "Black Panther" },  // Powers/Player/BlackPanther/Talents/JungleSnareRangedTalent.prototype
        {   2976u, "Taskmaster" },  // Powers/Player/Taskmaster/SerumConsume.prototype
        {   2978u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzyFinishingHit.prototype
        {   2981u, "Vision" },  // Powers/Player/Vision/SolarBolt.prototype
        {   2982u, "Storm" },  // Powers/Player/Storm/TyphoonRainSummonCombo.prototype
        {   2984u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/ForcePushPhoenixForceGain.prototype
        {   2985u, "Ghost Rider" },  // Powers/Player/GhostRider/ChainsOnFireDamageBuffEffect.prototype
        {   2988u, "Iron Man" },  // Powers/Player/IronMan/Talents/UpgradeCoolingSystem.prototype
        {   2989u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/ForceFieldGeneratorDeath.prototype
        {   2992u, "Ant-Man" },  // Powers/Player/AntMan/RapidShrinkStrikeDoTCombo.prototype
        {   2994u, "Deadpool" },  // Powers/Player/Deadpool/UltimateBuffComboEffect.prototype
        {   2995u, "Iron Man" },  // Powers/Player/IronMan/SpeedRushProcEffect.prototype
        {   2996u, "Iron Fist" },  // Powers/Player/IronFist/CraneStanceEnduranceMaterialOverride.prototype
        {   3000u, "Elektra" },  // Powers/Player/Elektra/KillCommandSummonMaster.prototype
        {   3006u, "Iron Man" },  // Powers/Player/IronMan/ShieldOverloadExplosionUpgrade.prototype
        {   3007u, "Wolverine" },  // Powers/Player/Wolverine/FrenzyStackingBuffTalent.prototype
        {   3010u, "Elektra" },  // Powers/Player/Elektra/StealthAsCombo.prototype
        {   3013u, "Vision" },  // Powers/Player/Vision/Traits/MechanicTrait.prototype
        {   3014u, "She-Hulk" },  // Powers/Player/SheHulk/Conviction.prototype
        {   3015u, "Deadpool" },  // Powers/Player/Deadpool/StopDropRollStunEffect.prototype
        {   3019u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/ConeYankNunchuckBulldozeBonus.prototype
        {   3022u, "Moon Knight" },  // Powers/Player/MoonKnight/ConeYank.prototype
        {   3024u, "Nova" },  // Powers/Player/Nova/Talents/Talent1ChanneledBeamDetonateBuff.prototype
        {   3031u, "Taskmaster" },  // Powers/Player/Taskmaster/SwappingPassiveDamageCritProc.prototype
        {   3033u, "Black Bolt" },  // Powers/Player/BlackBolt/GeyserHotspotEffect.prototype
        {   3034u, "War Machine" },  // Powers/Player/WarMachine/ChaingunBulletSprayHotspotEffect.prototype
        {   3037u, "Iron Man" },  // Powers/Player/IronMan/OrbitalBombardmentRandomStrikeController.prototype
        {   3038u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/InvisibleWomanInvisibility.prototype
        {   3040u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshuStatueTerrifyHotspotEffect.prototype
        {   3042u, "Beast" },  // Powers/Player/Beast/HulkingSlamJubileeHit.prototype
        {   3043u, "Iceman" },  // Powers/Player/Iceman/SpikePunch.prototype
        {   3045u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/KineticBoltPhoenixHealthRestore.prototype
        {   3046u, "Cyclops" },  // Powers/Player/Cyclops/Rework/ConeBeamHiddenPassive.prototype
        {   3047u, "Winter Soldier" },  // Powers/Player/WinterSoldier/ArmBlastCooldownProc.prototype
        {   3049u, "Hawkeye" },  // Powers/Player/Hawkeye/FreezeArrowSummonHotspot.prototype
        {   3051u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ConeRapidPunch.prototype
        {   3052u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelCounterUp.prototype
        {   3056u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LivingLaserLaserBlastEnd.prototype
        {   3058u, "Jean Grey" },  // Powers/Player/JeanGrey/PhoenixModeTogglePermanentBuffPhoenixHiddenPassi.prototype
        {   3060u, "Storm" },  // Powers/Player/Storm/StormSurge.prototype
        {   3065u, "Cable" },  // Powers/Player/Cable/Talents/ConcussionBlastLayer.prototype
        {   3066u, "Green Goblin" },  // Powers/Player/GreenGoblin/Talents/RazorBatsBleed.prototype
        {   3067u, "Nova" },  // Powers/Player/Nova/BasicSpiritBeam.prototype
        {   3069u, "Nightcrawler" },  // Powers/Player/Nightcrawler/UltimateDebris.prototype
        {   3072u, "Nick Fury" },  // Powers/Player/NickFury/MicroDronesRandomPosition.prototype
        {   3073u, "Luke Cage" },  // Powers/Player/LukeCage/ChargeEndEffect.prototype
        {   3075u, "Loki" },  // Powers/Player/Loki/SearingEmbersAttack.prototype
        {   3078u, "Wolverine" },  // Powers/Player/Wolverine/Traits/OffenseTrait.prototype
        {   3084u, "Iron Fist" },  // Powers/Player/IronFist/DragonStanceVisual.prototype
        {   3085u, "Angela" },  // Powers/Player/Angela/DFAEndAndResetProc.prototype
        {   3086u, "Ghost Rider" },  // Powers/Player/GhostRider/DeathFromAboveEnd.prototype
        {   3090u, "Cable" },  // Powers/Player/Cable/GreymalkinBombExplosion.prototype
        {   3091u, "Angela" },  // Powers/Player/Angela/SwordLungeRibbonBurst.prototype
        {   3093u, "Hawkeye" },  // Powers/Player/Hawkeye/BasicArrowExtraShot2.prototype
        {   3095u, "Iceman" },  // Powers/Player/Iceman/IcemanClonesRangedDefaultAttack.prototype
        {   3099u, "War Machine" },  // Powers/Player/WarMachine/HeatGainAutoGun.prototype
        {   3100u, "Kitty Pryde" },  // Powers/Player/KittyPryde/TagTeamSecondHit.prototype
        {   3103u, "Blade" },  // Powers/Player/Blade/Talents/SpecRotational.prototype
        {   3105u, "Daredevil" },  // Powers/Player/Daredevil/UltimateKickCombo2.prototype
        {   3106u, "Storm" },  // Powers/Player/Storm/HurricaneWinds.prototype
        {   3107u, "War Machine" },  // Powers/Player/WarMachine/EMP.prototype
        {   3110u, "Blade" },  // Powers/Player/Blade/BloodlustMaxedHighRiskHighlightSerum.prototype
        {   3115u, "Taskmaster" },  // Powers/Player/Taskmaster/ThreeRoundBurstExtraShot2.prototype
        {   3116u, "Iron Man" },  // Powers/Player/IronMan/RepulsorSpray.prototype
        {   3117u, "Iceman" },  // Powers/Player/Iceman/UltimateSummonHotspot.prototype
        {   3119u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotInfernoFlamethrower.prototype
        {   3121u, "Ant-Man" },  // Powers/Player/AntMan/NotSoBigPunch.prototype
        {   3122u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Talents/FlourishTeleportComboTalent.prototype
        {   3123u, "Magik" },  // Powers/Player/Magik/SummonLimboSpitter.prototype
        {   3125u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/NeuralNetworkPhoenixTransfer.prototype
        {   3128u, "Deadpool" },  // Powers/Player/Deadpool/Rework/ServerLagDoT.prototype
        {   3129u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/ColleenWing/ColleenWingDash.prototype
        {   3132u, "Blade" },  // Powers/Player/Blade/AdvancedTechniqueCombo1Stun.prototype
        {   3136u, "Hulk" },  // Powers/Player/Hulk/Rework/PassiveToughReviveShrinkGrowCond.prototype
        {   3142u, "Rogue" },  // Powers/Player/Rogue/StolenPowerLibrarySlot6.prototype
        {   3146u, "Black Panther" },  // Powers/Player/BlackPanther/DoraMilajeTaunt.prototype
        {   3148u, "Beast" },  // Powers/Player/Beast/PummelStartCombo.prototype
        {   3149u, "Venom" },  // Powers/Player/Venom/HealthPassive.prototype
        {   3151u, "Storm" },  // Powers/Player/Storm/Traits/MechanicTraitSurge.prototype
        {   3155u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Burrow.prototype
        {   3157u, "Psylocke" },  // Powers/Player/Psylocke/StealthMechanicHiddenPassiveProc.prototype
        {   3158u, "Taskmaster" },  // Powers/Player/Taskmaster/MeleeBuffProc.prototype
        {   3160u, "Magik" },  // Powers/Player/Magik/BoneSpiritOMeleeHitCharge.prototype
        {   3161u, "Hulk" },  // Powers/Player/Hulk/Rework/WorldbreakerHotspotSummon.prototype
        {   3165u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/SquirrelsFromAbove.prototype
        {   3171u, "Elektra" },  // Powers/Player/Elektra/BasicSai.prototype
        {   3172u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BamfHotspotSummonCombo.prototype
        {   3173u, "Green Goblin" },  // Powers/Player/GreenGoblin/MachineGuns.prototype
        {   3175u, "Loki" },  // Powers/Player/Loki/GlacialSpikeSlowCombo.prototype
        {   3176u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedToggleHiddenPassive.prototype
        {   3179u, "Green Goblin" },  // Powers/Player/GreenGoblin/HeatSeekingBatsMissileEffect.prototype
        {   3182u, "Venom" },  // Powers/Player/Venom/SigFreakoutWebAttachVisualCombo.prototype
        {   3185u, "Daredevil" },  // Powers/Player/Daredevil/BrutalStrikeEffect.prototype
        {   3186u, "Taskmaster" },  // Powers/Player/Taskmaster/PoisonGasBombProc.prototype
        {   3187u, "Luke Cage" },  // Powers/Player/LukeCage/SweetChristmasUltimateEffectPets.prototype
        {   3189u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ElectricAoEGadgetHotspotEffect.prototype
        {   3195u, "She-Hulk" },  // Powers/Player/SheHulk/UltimateInitialHit.prototype
        {   3199u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedChargeLockout.prototype
        {   3201u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/PBAoEKnockdownChargeCount.prototype
        {   3204u, "Silver Surfer" },  // Powers/Player/SilverSurfer/DisengageComboEffect.prototype
        {   3207u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateCallIcemanDamageEffect.prototype
        {   3208u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondStrike.prototype
        {   3209u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BamfHotspotVulnerability.prototype
        {   3210u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateBattleVan.prototype
        {   3212u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BoardSweepStackingBuff.prototype
        {   3214u, "Colossus" },  // Powers/Player/Colossus/KittyPrydeSummon/DefaultAttack4.prototype
        {   3216u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/BasicPistolsSpiritGainComboEffect.prototype
        {   3217u, "Psylocke" },  // Powers/Player/Psylocke/PassiveDecoysHiddenPassive.prototype
        {   3219u, "Juggernaut" },  // Powers/Player/Juggernaut/EarthquakeLeapVulnCombo.prototype
        {   3220u, "Iceman" },  // Powers/Player/Iceman/Talents/HotspotBeamHealing.prototype
        {   3221u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Ultimate.prototype
        {   3224u, "Doctor Strange" },  // Powers/Player/DoctorStrange/ProjectionIcyTendrils.prototype
        {   3225u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent1DamageMultWithNoArmorBuffRemover.prototype
        {   3227u, "Black Bolt" },  // Powers/Player/BlackBolt/ImplodeMelee.prototype
        {   3228u, "Captain America" },  // Powers/Player/CaptainAmerica/VibraniumStackCombo.prototype
        {   3231u, "Punisher" },  // Powers/Player/Punisher/Rework/AmmoCostComboBase.prototype
        {   3232u, "Deadpool" },  // Powers/Player/Deadpool/Rework/HulkHandArrow.prototype
        {   3235u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LukeCagePummel7thAttack.prototype
        {   3236u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickQuiverComboExplosiveArrowProc.prototype
        {   3237u, "Hawkeye" },  // Powers/Player/Hawkeye/FreezeArrowMissileEffect.prototype
        {   3240u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BrimstoneBlitz.prototype
        {   3242u, "Iron Man" },  // Powers/Player/IronMan/ShieldOverload.prototype
        {   3244u, "X-23" },  // Powers/Player/X23/MoveMechanicHiddenPassiveProc.prototype
        {   3245u, "X-23" },  // Powers/Player/X23/Talents/Talent3BladeSpinMvmtCrimsonLeapBleed.prototype
        {   3246u, "Punisher" },  // Powers/Player/Punisher/Rework/Kill.prototype
        {   3250u, "Juggernaut" },  // Powers/Player/Juggernaut/UnstoppableChargeHotspotDoT.prototype
        {   3253u, "Human Torch" },  // Powers/Player/HumanTorch/BowlingBallKnockbackMissileEffect.prototype
        {   3255u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/Implosion.prototype
        {   3258u, "Ultron" },  // Powers/Player/Ultron/CleanseSelfRez.prototype
        {   3259u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent1RegenArmor.prototype
        {   3260u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/DefenseHotspotKnockbackCombo.prototype
        {   3261u, "Green Goblin" },  // Powers/Player/GreenGoblin/GoblinBlast.prototype
        {   3263u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerCableViperBeamHotspotEffect.prototype
        {   3265u, "Storm" },  // Powers/Player/Storm/Talents/WindSpecDustDevilHitEffect.prototype
        {   3268u, "Magneto" },  // Powers/Player/Magneto/Talents/ElectromagneticCooldownResets.prototype
        {   3270u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BoardDashExtraBeamRight.prototype
        {   3271u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/GunTurretNuke.prototype
        {   3272u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent2DefenseBuffHealthTransfer.prototype
        {   3276u, "Blade" },  // Powers/Player/Blade/SerumInjectionRotationalBuff.prototype
        {   3279u, "Deadpool" },  // Powers/Player/Deadpool/Rework/BazookaExplosionSmall.prototype
        {   3282u, "Ultron" },  // Powers/Player/Ultron/BeginDetonation.prototype
        {   3290u, "Blade" },  // Powers/Player/Blade/Traits/BloodLustMechanicTrait.prototype
        {   3291u, "Colossus" },  // Powers/Player/Colossus/NoCallInSpecBuffRemover.prototype
        {   3296u, "Emma Frost" },  // Powers/Player/EmmaFrost/BasicSpiritGainComboEffect.prototype
        {   3298u, "Venom" },  // Powers/Player/Venom/Talents/InfectBuff.prototype
        {   3299u, "Beast" },  // Powers/Player/Beast/ShieldGadgetProc.prototype
        {   3304u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GravityMineEffect.prototype
        {   3305u, "Storm" },  // Powers/Player/Storm/MaelstromIntervalHotspotEffect.prototype
        {   3307u, "Thor" },  // Powers/Player/Thor/Talents/OdinforceRemovedTalent.prototype
        {   3310u, "Punisher" },  // Powers/Player/Punisher/Talents/PineappleGrenadeChemCancelSummonCombo.prototype
        {   3311u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/ShockwavePBAOEBonus.prototype
        {   3319u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ConeRapidPunchSlow.prototype
        {   3322u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MrSinisterAstralProjectionExplode.prototype
        {   3324u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ObfuscationKnockbackCombo.prototype
        {   3325u, "Nova" },  // Powers/Player/Nova/Traits/OffenseTrait.prototype
        {   3326u, "Juggernaut" },  // Powers/Player/Juggernaut/Ultimate.prototype
        {   3329u, "Human Torch" },  // Powers/Player/HumanTorch/FlameOn.prototype
        {   3330u, "Cable" },  // Powers/Player/Cable/TelepathicIllusionPlus.prototype
        {   3331u, "Psylocke" },  // Powers/Player/Psylocke/KickPunch.prototype
        {   3334u, "Magik" },  // Powers/Player/Magik/SoulShockwave.prototype
        {   3338u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MistressOfMagmaMentalBlast.prototype
        {   3339u, "Ghost Rider" },  // Powers/Player/GhostRider/UltimateBasicFireballDoTStack.prototype
        {   3340u, "Wolverine" },  // Powers/Player/Wolverine/CantKeepMeDownHealthTransfer.prototype
        {   3341u, "Daredevil" },  // Powers/Player/Daredevil/UltimateCritBuff.prototype
        {   3345u, "Colossus" },  // Powers/Player/Colossus/DeathFromAboveEnd.prototype
        {   3346u, "Magik" },  // Powers/Player/Magik/LimboDemonDoubleStrike.prototype
        {   3347u, "Dr. Doom" },  // Powers/Player/DrDoom/BallLightningMissileEffect.prototype
        {   3348u, "Black Panther" },  // Powers/Player/BlackPanther/RemoveDoraMilajeSummon.prototype
        {   3349u, "Deadpool" },  // Powers/Player/Deadpool/Rework/StrafeMissilePowerTargeted.prototype
        {   3354u, "Elektra" },  // Powers/Player/Elektra/TeleportDashNextHitProc.prototype
        {   3357u, "Luke Cage" },  // Powers/Player/LukeCage/Traits/MechanicTraitComboPoints.prototype
        {   3359u, "X-23" },  // Powers/Player/X23/PassiveStealthCDRHiddenPassive.prototype
        {   3362u, "Kitty Pryde" },  // Powers/Player/KittyPryde/DeathFromBelow.prototype
        {   3364u, "Vision" },  // Powers/Player/Vision/SolarCone.prototype
        {   3365u, "War Machine" },  // Powers/Player/WarMachine/OverheatEffect.prototype
        {   3366u, "Black Cat" },  // Powers/Player/BlackCat/ShrapnelTrapExplosion.prototype
        {   3367u, "Thing" },  // Powers/Player/Thing/Traits/ClobberinTimeBuff.prototype
        {   3368u, "War Machine" },  // Powers/Player/WarMachine/PlasmaRoundsCondition.prototype
        {   3369u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Talents/Talent1GrootRide.prototype
        {   3371u, "Black Bolt" },  // Powers/Player/BlackBolt/AutoRevive.prototype
        {   3372u, "Psylocke" },  // Powers/Player/Psylocke/Traits/BarrierAbsorbStopper.prototype
        {   3374u, "Luke Cage" },  // Powers/Player/LukeCage/ComboPointHiddenPassive.prototype
        {   3375u, "Nick Fury" },  // Powers/Player/NickFury/Talents/Talent3Bandolier.prototype
        {   3377u, "Punisher" },  // Powers/Player/Punisher/Rework/PineappleGrenade.prototype
        {   3378u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotHiddenPassiveDisabler.prototype
        {   3379u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Talents/Talent3DFACritIncrease.prototype
        {   3380u, "Taskmaster" },  // Powers/Player/Taskmaster/WidowsBiteProc.prototype
        {   3381u, "Daredevil" },  // Powers/Player/Daredevil/ShadowStrikeComboPointGain.prototype
        {   3384u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/UltimateFFFireHotspotSummon.prototype
        {   3385u, "Daredevil" },  // Powers/Player/Daredevil/UltimateKickCombo.prototype
        {   3387u, "Hawkeye" },  // Powers/Player/Hawkeye/UltimateInvulnCombo.prototype
        {   3389u, "Taskmaster" },  // Powers/Player/Taskmaster/UltimateBlackWidowTwilightInitiative.prototype
        {   3390u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedChargeReturn.prototype
        {   3393u, "Iron Man" },  // Powers/Player/IronMan/Talents/UpgradeTargetingComputer.prototype
        {   3395u, "Vision" },  // Powers/Player/Vision/Talents/Talent5SigSolarEnergyRegen.prototype
        {   3396u, "She-Hulk" },  // Powers/Player/SheHulk/FinalVerdict.prototype
        {   3397u, "Cable" },  // Powers/Player/Cable/VortexGrenadeSummonCombo.prototype
        {   3399u, "Venom" },  // Powers/Player/Venom/RangedPassiveProcEffect.prototype
        {   3401u, "Blade" },  // Powers/Player/Blade/ShotgunMissileEffectBonusCrit.prototype
        {   3404u, "Angela" },  // Powers/Player/Angela/AngelaTreeCDProc.prototype
        {   3406u, "Nick Fury" },  // Powers/Player/NickFury/Talents/Talent4FireteamMinigun.prototype
        {   3407u, "Storm" },  // Powers/Player/Storm/UltimateCombo.prototype
        {   3408u, "Taskmaster" },  // Powers/Player/Taskmaster/WhirlingClub.prototype
        {   3411u, "Daredevil" },  // Powers/Player/Daredevil/Talents/NoComboPointsTalent.prototype
        {   3413u, "Hawkeye" },  // Powers/Player/Hawkeye/UltimateDoTProc.prototype
        {   3416u, "Thing" },  // Powers/Player/Thing/Rework/CallStretchSummonCombo.prototype
        {   3419u, "War Machine" },  // Powers/Player/WarMachine/ChaingunBurstMissileEffectPlasma.prototype
        {   3423u, "Daredevil" },  // Powers/Player/Daredevil/CritPassiveProcEffect.prototype
        {   3427u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent1IncreaseMaxComboPoints.prototype
        {   3428u, "Green Goblin" },  // Powers/Player/GreenGoblin/BombingCircle.prototype
        {   3429u, "Angela" },  // Powers/Player/Angela/HackSlash.prototype
        {   3436u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexBoltMissileEffect.prototype
        {   3438u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickVolleyShriekingArrowCombo.prototype
        {   3444u, "Dr. Doom" },  // Powers/Player/DrDoom/MagicOrbSummon.prototype
        {   3445u, "Punisher" },  // Powers/Player/Punisher/Talents/IncendiaryGrenades.prototype
        {   3449u, "Juggernaut" },  // Powers/Player/Juggernaut/Traits/MechanicTrait.prototype
        {   3451u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondHeart.prototype
        {   3452u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ChanneledBeam.prototype
        {   3456u, "Cyclops" },  // Powers/Player/Cyclops/Rework/ChargeConeSmall.prototype
        {   3458u, "Green Goblin" },  // Powers/Player/GreenGoblin/SonicToadDeathProc.prototype
        {   3460u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/UltimateAcornMeteorEffect.prototype
        {   3461u, "Colossus" },  // Powers/Player/Colossus/WolverineSummon/DefaultAttack3.prototype
        {   3462u, "Angela" },  // Powers/Player/Angela/DeathFromAboveEnduranceGain.prototype
        {   3463u, "Beast" },  // Powers/Player/Beast/TeslaTowerGadgetSummon.prototype
        {   3467u, "Nick Fury" },  // Powers/Player/NickFury/Talents/Talent4ExecuteCooldownReset.prototype
        {   3468u, "Magneto" },  // Powers/Player/Magneto/UltimateSentinelSmashExplosion.prototype
        {   3474u, "Iron Man" },  // Powers/Player/IronMan/BrutalStrikeComboEffect.prototype
        {   3475u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/TelepathyHiddenPassiveDiamondVersionCombo.prototype
        {   3476u, "Carnage" },  // Powers/Player/Carnage/KnifeBarrageHotspotEffect.prototype
        {   3480u, "Thing" },  // Powers/Player/Thing/Rework/CallStretchHotspotEffect.prototype
        {   3484u, "Deadpool" },  // Powers/Player/Deadpool/Rework/StabbyFlipAttack.prototype
        {   3485u, "Green Goblin" },  // Powers/Player/GreenGoblin/Traits/DefenseTrait.prototype
        {   3486u, "Iceman" },  // Powers/Player/Iceman/ChanneledBeamIntervalEffect.prototype
        {   3487u, "Doctor Strange" },  // Powers/Player/DoctorStrange/WindsOfWatoombAstralFormRemovalCombo.prototype
        {   3490u, "Elektra" },  // Powers/Player/Elektra/SaiStrike.prototype
        {   3491u, "Magneto" },  // Powers/Player/Magneto/RapidFire.prototype
        {   3494u, "Silver Surfer" },  // Powers/Player/SilverSurfer/SingularityEffect.prototype
        {   3496u, "Captain America" },  // Powers/Player/CaptainAmerica/TeamCleanseCombo.prototype
        {   3498u, "Black Cat" },  // Powers/Player/BlackCat/ClawPummel5.prototype
        {   3502u, "Ant-Man" },  // Powers/Player/AntMan/FireAntVulnerabilityCombo.prototype
        {   3503u, "Daredevil" },  // Powers/Player/Daredevil/ConeYankComboPointGain.prototype
        {   3506u, "Kitty Pryde" },  // Powers/Player/KittyPryde/UltimateComboExtraProjectiles.prototype
        {   3507u, "Luke Cage" },  // Powers/Player/LukeCage/GoodAtCombosBrutalChanceBuff.prototype
        {   3508u, "Punisher" },  // Powers/Player/Punisher/Talents/InYourFace.prototype
        {   3509u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/BigBeamLayer.prototype
        {   3512u, "Luke Cage" },  // Powers/Player/LukeCage/PummelNoFinisherBuff.prototype
        {   3517u, "Elektra" },  // Powers/Player/Elektra/MarkForDeath.prototype
        {   3518u, "Nick Fury" },  // Powers/Player/NickFury/ShotgunAgentAttack.prototype
        {   3522u, "Cyclops" },  // Powers/Player/Cyclops/DisengagingShotMissileCombo.prototype
        {   3534u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Talents/Talent1LockheedActive.prototype
        {   3535u, "Emma Frost" },  // Powers/Player/EmmaFrost/AmpControlledMobComboProc.prototype
        {   3537u, "Colossus" },  // Powers/Player/Colossus/NightcrawlerSummon/DeathFromAboveEnd.prototype
        {   3538u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseToggleOutOfCombatCombo.prototype
        {   3541u, "Green Goblin" },  // Powers/Player/GreenGoblin/RazorBat.prototype
        {   3542u, "Magneto" },  // Powers/Player/Magneto/ShrapnelHotspotEffect.prototype
        {   3543u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveWizard.prototype
        {   3544u, "Nick Fury" },  // Powers/Player/NickFury/ChanneledEnergyBeamEffect.prototype
        {   3545u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/ArcTurretTaserSummonArea.prototype
        {   3549u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ObfuscationHotspotSlowEffect.prototype
        {   3551u, "Thor" },  // Powers/Player/Thor/Rework/HammerDashNormalCombo.prototype
        {   3552u, "Iceman" },  // Powers/Player/Iceman/ShowOffStatueExplosion.prototype
        {   3553u, "Taskmaster" },  // Powers/Player/Taskmaster/StudentsPMCRiotPetTauntCombo.prototype
        {   3556u, "Ultron" },  // Powers/Player/Ultron/Talents/Talent4BigBlastBuff.prototype
        {   3560u, "Jean Grey" },  // Powers/Player/JeanGrey/DrainDarkPhoenixExtraHealing.prototype
        {   3561u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/NeuralNetworkPhoenix.prototype
        {   3562u, "Venom" },  // Powers/Player/Venom/WebSplatVulnerabilityEffect.prototype
        {   3563u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/ForcePushReflectComboEffect.prototype
        {   3564u, "Ultron" },  // Powers/Player/Ultron/UltimateSuicideSummon.prototype
        {   3565u, "Punisher" },  // Powers/Player/Punisher/Rework/ChemicalBombLauncher.prototype
        {   3567u, "She-Hulk" },  // Powers/Player/SheHulk/SurpriseWitness.prototype
        {   3568u, "Thor" },  // Powers/Player/Thor/Rework/GroundSmash.prototype
        {   3570u, "Iron Fist" },  // Powers/Player/IronFist/FlyingKick.prototype
        {   3573u, "Storm" },  // Powers/Player/Storm/Talents/LightningTempest.prototype
        {   3574u, "Cable" },  // Powers/Player/Cable/RollEffect.prototype
        {   3575u, "Winter Soldier" },  // Powers/Player/WinterSoldier/ArmBlastReloadBuffCombo.prototype
        {   3577u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/FireWedgeConsumer.prototype
        {   3581u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent1AlwaysAngry.prototype
        {   3584u, "Nova" },  // Powers/Player/Nova/PBAoENukeHealingCombo.prototype
        {   3585u, "Jean Grey" },  // Powers/Player/JeanGrey/UltimateHotspotEffect.prototype
        {   3590u, "Winter Soldier" },  // Powers/Player/WinterSoldier/SpinningMineExplosion.prototype
        {   3592u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateCallinLockout.prototype
        {   3593u, "Hulk" },  // Powers/Player/Hulk/Rework/Tantrum1stAttack.prototype
        {   3594u, "Thor" },  // Powers/Player/Thor/Talents/ForAsgardBeserkerTalent.prototype
        {   3595u, "Wolverine" },  // Powers/Player/Wolverine/Talents/Talent3PassiveDmg.prototype
        {   3596u, "Elektra" },  // Powers/Player/Elektra/MarkForDeathCrossStrikeCombo.prototype
        {   3598u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GrimReaperEnergyBlastMissileEffe.prototype
        {   3600u, "Black Cat" },  // Powers/Player/BlackCat/Traits/MechanicTraitNineLives.prototype
        {   3603u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/BafflingDialogue.prototype
        {   3609u, "Loki" },  // Powers/Player/Loki/JotunFleshProcEffect.prototype
        {   3610u, "Iceman" },  // Powers/Player/Iceman/ArcticChillRegenProc.prototype
        {   3611u, "Colossus" },  // Powers/Player/Colossus/NightcrawlerSummon/DeathFromAbove.prototype
        {   3612u, "Gambit" },  // Powers/Player/Gambit/GrandSlamEnd.prototype
        {   3616u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/SpeedRushPhoenixEndCombo.prototype
        {   3617u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/DefensiveSpec.prototype
        {   3618u, "Daredevil" },  // Powers/Player/Daredevil/Talents/DamageCritBrutBuffTalent.prototype
        {   3629u, "Gambit" },  // Powers/Player/Gambit/CheatDeathSelfRez.prototype
        {   3630u, "Magik" },  // Powers/Player/Magik/LimboDemonDoubleStrike2ndHit.prototype
        {   3635u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/CosmicWake.prototype
        {   3637u, "War Machine" },  // Powers/Player/WarMachine/EMPChaingunLockout.prototype
        {   3643u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/DrDoomBallLightningBeam.prototype
        {   3644u, "Daredevil" },  // Powers/Player/Daredevil/NextFinisherFreeProcEffect.prototype
        {   3648u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/ImplosionPhoenixPullCombo.prototype
        {   3654u, "Venom" },  // Powers/Player/Venom/ConeDrainHealthOnKillBuff.prototype
        {   3656u, "Green Goblin" },  // Powers/Player/GreenGoblin/GasPumpkinExplosionEffect.prototype
        {   3657u, "Taskmaster" },  // Powers/Player/Taskmaster/StudentsPMCDemolitionChargeBoom.prototype
        {   3663u, "Hulk" },  // Powers/Player/Hulk/Traits/AngerFullRemoval.prototype
        {   3665u, "Luke Cage" },  // Powers/Player/LukeCage/PummelNoFinisher4thAttack.prototype
        {   3667u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent3ThrowRockBonus.prototype
        {   3668u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/Talent1ChaosBuffCDR.prototype
        {   3671u, "Black Widow" },  // Powers/Player/BlackWidow/RapidShotTargetAudioCombo.prototype
        {   3672u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ImplosionGadgetVulnerabilityComb.prototype
        {   3674u, "Deadpool" },  // Powers/Player/Deadpool/PowerUpsHealEffect.prototype
        {   3675u, "Emma Frost" },  // Powers/Player/EmmaFrost/UltimateRobotGroundSmash.prototype
        {   3676u, "Cable" },  // Powers/Player/Cable/PsimitarCycloneKeywordConditionCombo.prototype
        {   3677u, "Iron Fist" },  // Powers/Player/IronFist/BlackBlackPoisonTouchHotspotEffect.prototype
        {   3679u, "Emma Frost" },  // Powers/Player/EmmaFrost/TelepathyActiveEnemyEffectVisualOnly.prototype
        {   3680u, "Angela" },  // Powers/Player/Angela/WhippingRibbonsEnduranceGain.prototype
        {   3690u, "Thor" },  // Powers/Player/Thor/UltimateBuffEffect.prototype
        {   3691u, "Iron Man" },  // Powers/Player/IronMan/BoltSprayStunCombo.prototype
        {   3692u, "Cable" },  // Powers/Player/Cable/PsimitarLunge.prototype
        {   3693u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ArachneBouncingWeb.prototype
        {   3695u, "Iron Man" },  // Powers/Player/IronMan/Talents/ReactiveArmor.prototype
        {   3697u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/OrbStormSlowVulnerability.prototype
        {   3701u, "Ultron" },  // Powers/Player/Ultron/Yank.prototype
        {   3702u, "Cable" },  // Powers/Player/Cable/UltimateHiddenPassive.prototype
        {   3703u, "Angela" },  // Powers/Player/Angela/Talents/AxeBuffs.prototype
        {   3704u, "Winter Soldier" },  // Powers/Player/WinterSoldier/KnifeThrowDoT.prototype
        {   3706u, "She-Hulk" },  // Powers/Player/SheHulk/FinalVerdictEnd.prototype
        {   3707u, "Black Bolt" },  // Powers/Player/BlackBolt/Traits/DefenseTrait.prototype
        {   3708u, "Luke Cage" },  // Powers/Player/LukeCage/Talents/BoxingSpec.prototype
        {   3709u, "Black Panther" },  // Powers/Player/BlackPanther/Talents/AcrobaticAttackBuffTalent.prototype
        {   3710u, "X-23" },  // Powers/Player/X23/CoupDeGraceEnabled.prototype
        {   3712u, "Deadpool" },  // Powers/Player/Deadpool/HealthRegenPassiveProc.prototype
        {   3713u, "Winter Soldier" },  // Powers/Player/WinterSoldier/ArmBlastSecondaryResourceAdd.prototype
        {   3715u, "Thing" },  // Powers/Player/Thing/Rework/YancyStreetGangPieChuck.prototype
        {   3716u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/BouncingBubbleTalentChainEffect.prototype
        {   3717u, "Elektra" },  // Powers/Player/Elektra/Talents/TripleChainCDReset.prototype
        {   3718u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoRecurringShot.prototype
        {   3719u, "Doctor Strange" },  // Powers/Player/DoctorStrange/SummonFlamesHotspotEffect.prototype
        {   3722u, "Doctor Strange" },  // Powers/Player/DoctorStrange/MirrorImageTaunt.prototype
        {   3723u, "Iron Fist" },  // Powers/Player/IronFist/CraneStanceSecondHit.prototype
        {   3726u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/UltimateFuriousLungeProcEffect.prototype
        {   3729u, "Captain America" },  // Powers/Player/CaptainAmerica/FuriousLunge.prototype
        {   3732u, "Juggernaut" },  // Powers/Player/Juggernaut/AvatarOfCyttorakAsCombo.prototype
        {   3736u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Talents/Talent3ExecuteAlwaysBrutal.prototype
        {   3742u, "Punisher" },  // Powers/Player/Punisher/Rework/RPGMissileEffect.prototype
        {   3744u, "Nick Fury" },  // Powers/Player/NickFury/LowHealthProc.prototype
        {   3746u, "War Machine" },  // Powers/Player/WarMachine/WarMachineArmorHiddenPassive.prototype
        {   3748u, "Rogue" },  // Powers/Player/Rogue/DrainLifeHealingCombo.prototype
        {   3749u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/RangedSpec.prototype
        {   3750u, "Ant-Man" },  // Powers/Player/AntMan/NotSoBigPunchDamageMultForAntPunch.prototype
        {   3751u, "War Machine" },  // Powers/Player/WarMachine/AlphaStrike.prototype
        {   3752u, "Magik" },  // Powers/Player/Magik/BoneSpiritOnKillCharge.prototype
        {   3756u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickQuiverRandomizerProc.prototype
        {   3757u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/ClawSpec.prototype
        {   3758u, "Nova" },  // Powers/Player/Nova/PulsarKillComboAoE.prototype
        {   3759u, "Ultron" },  // Powers/Player/Ultron/Slam.prototype
        {   3762u, "Taskmaster" },  // Powers/Player/Taskmaster/SwingingAssault.prototype
        {   3766u, "Thing" },  // Powers/Player/Thing/Talents/Talent4CallStretchBuff.prototype
        {   3769u, "Black Widow" },  // Powers/Player/BlackWidow/FlipKick.prototype
        {   3771u, "Hawkeye" },  // Powers/Player/Hawkeye/ShriekingArrowExplosion.prototype
        {   3772u, "Kitty Pryde" },  // Powers/Player/KittyPryde/DeathFromBelowDamageShieldCombo.prototype
        {   3773u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BetaRayBillLightningBarrageBuffProc.prototype
        {   3776u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BetaRayBillLightningBarrage.prototype
        {   3777u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/SquirrelTwirlBonus.prototype
        {   3782u, "Iceman" },  // Powers/Player/Iceman/AbsoluteZeroSummonClones.prototype
        {   3784u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/RogueQuakeBeamHotspotEffect.prototype
        {   3788u, "Loki" },  // Powers/Player/Loki/MagicCrush.prototype
        {   3790u, "Thor" },  // Powers/Player/Thor/ImmortalCombatRestoreSpiritGainPerHit.prototype
        {   3792u, "Ghost Rider" },  // Powers/Player/GhostRider/UltimateSummonFirePillars.prototype
        {   3794u, "Green Goblin" },  // Powers/Player/GreenGoblin/PBAoESpinReflectAreaSummon.prototype
        {   3796u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelAttack.prototype
        {   3799u, "Rogue" },  // Powers/Player/Rogue/RogueBeastmodeCounter.prototype
        {   3803u, "Iron Man" },  // Powers/Player/IronMan/WristRocketMissileEffectDoT.prototype
        {   3805u, "Nick Fury" },  // Powers/Player/NickFury/MolecularGrenadeKeywordConditionCombo.prototype
        {   3806u, "Colossus" },  // Powers/Player/Colossus/UltimateComboInvulnerable.prototype
        {   3808u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent5CooldownReduction.prototype
        {   3809u, "Iceman" },  // Powers/Player/Iceman/Talents/DFAArmorSpend.prototype
        {   3811u, "Rogue" },  // Powers/Player/Rogue/UltimateRaginCajunTooltip.prototype
        {   3812u, "Iceman" },  // Powers/Player/Iceman/IceGolemHiddenPassiveRangedWasLast.prototype
        {   3814u, "Human Torch" },  // Powers/Player/HumanTorch/ChanneledEnergyBeamHotspotEffect.prototype
        {   3820u, "Carnage" },  // Powers/Player/Carnage/MegaClawSummonHotspot.prototype
        {   3822u, "Magik" },  // Powers/Player/Magik/Talents/Talent3MagicalProjection.prototype
        {   3827u, "Black Bolt" },  // Powers/Player/BlackBolt/WeakenVulnDualConditionCombo.prototype
        {   3828u, "Storm" },  // Powers/Player/Storm/ElementalStormHotspotVulnerability.prototype
        {   3829u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent4ObjectionMoveToStrikeTalent.prototype
        {   3830u, "Wolverine" },  // Powers/Player/Wolverine/PassiveFeralHealProc.prototype
        {   3831u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TKTossPhoenixMoreMissiles.prototype
        {   3832u, "Gambit" },  // Powers/Player/Gambit/Talents/Talent5AceOfClubs.prototype
        {   3836u, "Iceman" },  // Powers/Player/Iceman/ChanneledBeamEnhanced.prototype
        {   3838u, "Luke Cage" },  // Powers/Player/LukeCage/Pummel2ndAttack.prototype
        {   3839u, "She-Hulk" },  // Powers/Player/SheHulk/Traits/OffenseTrait.prototype
        {   3840u, "X-23" },  // Powers/Player/X23/GrievousWoundsSingleTarget.prototype
        {   3842u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelRapidFire.prototype
        {   3845u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SauronSwoopingFlames.prototype
        {   3847u, "Hulk" },  // Powers/Player/Hulk/Rework/Shockwave.prototype
        {   3848u, "Black Panther" },  // Powers/Player/BlackPanther/DoraMiliajeDefaultAttackCombo2.prototype
        {   3850u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/UnmakeRealityMobInstaKill.prototype
        {   3852u, "Vision" },  // Powers/Player/Vision/StealthToggle.prototype
        {   3854u, "Venom" },  // Powers/Player/Venom/RangedPassive.prototype
        {   3860u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/TumbleHaste.prototype
        {   3861u, "Captain America" },  // Powers/Player/CaptainAmerica/TwoSerumPointGainCombo.prototype
        {   3867u, "Colossus" },  // Powers/Player/Colossus/FastballSpecial.prototype
        {   3869u, "Ultron" },  // Powers/Player/Ultron/SummonRangedDroneProcEffect.prototype
        {   3871u, "Human Torch" },  // Powers/Player/HumanTorch/BowlingBallHotspots.prototype
        {   3877u, "Black Cat" },  // Powers/Player/BlackCat/BadLuckKnockDownEffect.prototype
        {   3879u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/UltimateHiddenPassiveRanks.prototype
        {   3880u, "X-23" },  // Powers/Player/X23/Pummel2.prototype
        {   3882u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/ColleenWing/BladeDance.prototype
        {   3883u, "Black Bolt" },  // Powers/Player/BlackBolt/AuraBuffEffect.prototype
        {   3885u, "Hulk" },  // Powers/Player/Hulk/Rework/Clap.prototype
        {   3887u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent5LawyerUpSuperBuff.prototype
        {   3894u, "Punisher" },  // Powers/Player/Punisher/Talents/CombatKnife.prototype
        {   3895u, "Rogue" },  // Powers/Player/Rogue/RapidPunchDashComboSummon.prototype
        {   3896u, "Rogue" },  // Powers/Player/Rogue/UltimateBamf.prototype
        {   3898u, "Ultron" },  // Powers/Player/Ultron/SummonSuicideDrone.prototype
        {   3900u, "Luke Cage" },  // Powers/Player/LukeCage/Traits/DefenseTrait.prototype
        {   3901u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent2ColossalWhirlwind.prototype
        {   3904u, "Beast" },  // Powers/Player/Beast/TeamworkSynergyEffect6s.prototype
        {   3906u, "X-23" },  // Powers/Player/X23/BladeSpinMovement.prototype
        {   3910u, "Venom" },  // Powers/Player/Venom/SymbioteDrainHiddenPassive.prototype
        {   3911u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/IronFistTechniqueTigerStanceBuff.prototype
        {   3912u, "Black Widow" },  // Powers/Player/BlackWidow/RoundhouseKickCombo.prototype
        {   3915u, "Punisher" },  // Powers/Player/Punisher/Rework/Flamethrower.prototype
        {   3917u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/BurrowHasteCombo.prototype
        {   3918u, "She-Hulk" },  // Powers/Player/SheHulk/FuriousLunge.prototype
        {   3924u, "Luke Cage" },  // Powers/Player/LukeCage/UltimatePetBuff.prototype
        {   3928u, "Black Widow" },  // Powers/Player/BlackWidow/RemoteDetonatorNew.prototype
        {   3929u, "Juggernaut" },  // Powers/Player/Juggernaut/Traits/DefenseTrait.prototype
        {   3935u, "Cyclops" },  // Powers/Player/Cyclops/DisengagingShotMissileEffect.prototype
        {   3937u, "Iron Man" },  // Powers/Player/IronMan/BrutalStrike.prototype
        {   3942u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/ColossusFastballSpecialMissileEffect.prototype
        {   3944u, "Dr. Doom" },  // Powers/Player/DrDoom/AoEDebuff.prototype
        {   3953u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzy.prototype
        {   3954u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/StretchyBrain.prototype
        {   3956u, "War Machine" },  // Powers/Player/WarMachine/ThermiteRoundsProcEffect.prototype
        {   3957u, "Nightcrawler" },  // Powers/Player/Nightcrawler/ValiantLeap.prototype
        {   3962u, "Iron Man" },  // Powers/Player/IronMan/UltimateSuitWristRocketDoT.prototype
        {   3964u, "X-23" },  // Powers/Player/X23/Execute.prototype
        {   3965u, "Green Goblin" },  // Powers/Player/GreenGoblin/Rockets.prototype
        {   3966u, "Black Widow" },  // Powers/Player/BlackWidow/TumbleSprint.prototype
        {   3967u, "Taskmaster" },  // Powers/Player/Taskmaster/SwordSlice3rdHit.prototype
        {   3968u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/ShieldBoostTalentCombo.prototype
        {   3969u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/JessicaJones/JessicaJonesDefaultAttack3.prototype
        {   3970u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCardMissileEffectStage1.prototype
        {   3971u, "Green Goblin" },  // Powers/Player/GreenGoblin/GoblinLaserHotspotEffect.prototype
        {   3973u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Talents/Talent2SigCDR.prototype
        {   3974u, "Captain America" },  // Powers/Player/CaptainAmerica/ShieldThrowPBAoE.prototype
        {   3976u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TKTossJeanMoreMissiles.prototype
        {   3980u, "Black Widow" },  // Powers/Player/BlackWidow/Punch.prototype
        {   3981u, "Elektra" },  // Powers/Player/Elektra/MarkForDeathRangedAOE200Combo.prototype
        {   3983u, "Elektra" },  // Powers/Player/Elektra/KillCommandMasterSlash2.prototype
        {   3987u, "Venom" },  // Powers/Player/Venom/WrithingTendrilsMissileEffect.prototype
        {   3988u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SurturSwordAttackHotspots.prototype
        {   3989u, "Storm" },  // Powers/Player/Storm/ChanneledLightning.prototype
        {   3991u, "Black Panther" },  // Powers/Player/BlackPanther/SmokeScreenAllyBuffEffect.prototype
        {   3997u, "Blade" },  // Powers/Player/Blade/LongLivedHiddenPassive.prototype
        {   3998u, "Magik" },  // Powers/Player/Magik/DarkPactDemonBuff.prototype
        {   4003u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveThingClobberinTimeDecay.prototype
        {   4006u, "Punisher" },  // Powers/Player/Punisher/Rework/ThreeRoundBurst.prototype
        {   4007u, "Thing" },  // Powers/Player/Thing/Rework/YancyStreetGangPiePrank.prototype
        {   4011u, "Loki" },  // Powers/Player/Loki/UltimateHiddenPassiveBuff.prototype
        {   4013u, "Iron Fist" },  // Powers/Player/IronFist/BlackBlackPoisonTouchEffectSnake.prototype
        {   4015u, "War Machine" },  // Powers/Player/WarMachine/FlameThrower.prototype
        {   4017u, "Juggernaut" },  // Powers/Player/Juggernaut/BigCharge.prototype
        {   4019u, "Kitty Pryde" },  // Powers/Player/KittyPryde/HeartCrushCombo.prototype
        {   4021u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/BubbleSpray.prototype
        {   4023u, "Psylocke" },  // Powers/Player/Psylocke/DashStealthPowerEffectDecoyPower.prototype
        {   4024u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/ColleenWing/ColleenWingDashProc.prototype
        {   4027u, "Black Widow" },  // Powers/Player/BlackWidow/RapidBiteTargetAudioCombo.prototype
        {   4028u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/GunTurretChargeBeamMissileEffect.prototype
        {   4032u, "Punisher" },  // Powers/Player/Punisher/Rework/SawedOff.prototype
        {   4035u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/BasicRangedSquirrelPiercingMissileEffect.prototype
        {   4036u, "Wolverine" },  // Powers/Player/Wolverine/BerserkerBarrageHotspotKnockback.prototype
        {   4037u, "Cable" },  // Powers/Player/Cable/FutureBomb.prototype
        {   4042u, "Black Widow" },  // Powers/Player/BlackWidow/WidowsKiss.prototype
        {   4045u, "Iceman" },  // Powers/Player/Iceman/IceGolemSnowstorm.prototype
        {   4047u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/CrimsonForceFieldTeamBuff.prototype
        {   4049u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerSummonKidPirate.prototype
        {   4050u, "Angela" },  // Powers/Player/Angela/SignatureEndAndResetProc.prototype
        {   4051u, "Ant-Man" },  // Powers/Player/AntMan/TankerThrowHotspotEffect.prototype
        {   4052u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/AmpControlledMobCDR.prototype
        {   4054u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveFalcon.prototype
        {   4059u, "Ultron" },  // Powers/Player/Ultron/Signature.prototype
        {   4065u, "Iron Fist" },  // Powers/Player/IronFist/Pummel1stAttack.prototype
        {   4069u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GhostRiderFireBreathDoTHotspotEf.prototype
        {   4074u, "Green Goblin" },  // Powers/Player/GreenGoblin/MachineGunsMovementBuff.prototype
        {   4075u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseDashAlternateMissile.prototype
        {   4076u, "Daredevil" },  // Powers/Player/Daredevil/Update/CaneAttackEnduranceRegen.prototype
        {   4077u, "Vision" },  // Powers/Player/Vision/FocusBeam.prototype
        {   4079u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BoardSweepCDR.prototype
        {   4083u, "Ultron" },  // Powers/Player/Ultron/DroneStrafeMissile.prototype
        {   4086u, "Taskmaster" },  // Powers/Player/Taskmaster/UltimateDaredevilShadowStrikeDrop.prototype
        {   4089u, "Venom" },  // Powers/Player/Venom/SymbioteDrainPowerGreen2.prototype
        {   4090u, "Venom" },  // Powers/Player/Venom/Talents/IchorCostReduction.prototype
        {   4091u, "Iron Fist" },  // Powers/Player/IronFist/PummelStartMove.prototype
        {   4094u, "Ultron" },  // Powers/Player/Ultron/UltronDronePassive.prototype
        {   4097u, "Captain America" },  // Powers/Player/CaptainAmerica/HeroicStrikeShieldSwipeStackingCondition.prototype
        {   4099u, "Black Widow" },  // Powers/Player/BlackWidow/UltimateDamageEffect.prototype
        {   4100u, "X-23" },  // Powers/Player/X23/ExecuteStunCombo.prototype
        {   4107u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ChanneledBeamPhase2Loop.prototype
        {   4108u, "Iron Man" },  // Powers/Player/IronMan/ShieldOverloadHealing.prototype
        {   4112u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/MistyKnight/MistyKnightFollowupShot2.prototype
        {   4113u, "Jean Grey" },  // Powers/Player/JeanGrey/Ultimate.prototype
        {   4114u, "Hulk" },  // Powers/Player/Hulk/Rework/Rawr.prototype
        {   4115u, "Carnage" },  // Powers/Player/Carnage/TransfusionFullTransferBuffCombo.prototype
        {   4119u, "X-23" },  // Powers/Player/X23/FerocityStack.prototype
        {   4120u, "Blade" },  // Powers/Player/Blade/UnleashGlaiveCooldown.prototype
        {   4121u, "Taskmaster" },  // Powers/Player/Taskmaster/BrutalStrikeSecondaryResourceGain.prototype
        {   4122u, "Black Bolt" },  // Powers/Player/BlackBolt/HypersonicScreamHotspotEffect.prototype
        {   4123u, "Hulk" },  // Powers/Player/Hulk/Rework/RawrHealthGain.prototype
        {   4124u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GrootOutThornsProc.prototype
        {   4128u, "Iceman" },  // Powers/Player/Iceman/ShowoffPassiveTauntActivation.prototype
        {   4129u, "Thing" },  // Powers/Player/Thing/Rework/RockyPunchSpiritCombo.prototype
        {   4130u, "Taskmaster" },  // Powers/Player/Taskmaster/VolleyConeDamage.prototype
        {   4131u, "Winter Soldier" },  // Powers/Player/WinterSoldier/PistolShot.prototype
        {   4135u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/RangedSplashShotVulnMissileEffect.prototype
        {   4137u, "Doctor Strange" },  // Powers/Player/DoctorStrange/BaiscBoltsBuff.prototype
        {   4139u, "Loki" },  // Powers/Player/Loki/SorcerousBlast.prototype
        {   4141u, "Thor" },  // Powers/Player/Thor/Rework/HammerDashOdinforceCombo.prototype
        {   4142u, "Dr. Doom" },  // Powers/Player/DrDoom/ServoGuardMissileEffect.prototype
        {   4143u, "Beast" },  // Powers/Player/Beast/BeastBamfAreaAoEHitSynergized.prototype
        {   4144u, "War Machine" },  // Powers/Player/WarMachine/Talents/Talent5StealthSuit.prototype
        {   4148u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BasicStealthPunchBonusCombo.prototype
        {   4149u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexBolt.prototype
        {   4151u, "Colossus" },  // Powers/Player/Colossus/PickUpTerrainMissileEffect.prototype
        {   4152u, "Thing" },  // Powers/Player/Thing/Rework/CrashingLeapVulnerabilityCombo.prototype
        {   4153u, "Captain America" },  // Powers/Player/CaptainAmerica/BrutalStrike.prototype
        {   4155u, "Deadpool" },  // Powers/Player/Deadpool/CleverGirlDollDeathEffect.prototype
        {   4156u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CosmicGiftAccuracy.prototype
        {   4157u, "Vision" },  // Powers/Player/Vision/ScorchedEarthHotspotEffect.prototype
        {   4159u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshuSteroidHealthTauntEffect.prototype
        {   4160u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/GunTurretAtkSpdStarter.prototype
        {   4161u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/AlternatingMomentumFinishers.prototype
        {   4163u, "Hulk" },  // Powers/Player/Hulk/Rework/LeapImplodeMeleeBuffCombo.prototype
        {   4165u, "Ultron" },  // Powers/Player/Ultron/BladeDroneSpinAttack.prototype
        {   4166u, "Magneto" },  // Powers/Player/Magneto/SpawnMetalOrbProc.prototype
        {   4167u, "Cable" },  // Powers/Player/Cable/TechnoOrganicVirusBuff.prototype
        {   4168u, "Venom" },  // Powers/Player/Venom/PBAoEBlobFilterPower.prototype
        {   4170u, "Thing" },  // Powers/Player/Thing/CrushingLeapEnd.prototype
        {   4172u, "Venom" },  // Powers/Player/Venom/UltimateSymbioteDrainPower3.prototype
        {   4174u, "Nova" },  // Powers/Player/Nova/PulsarProximityBuff.prototype
        {   4175u, "Dr. Doom" },  // Powers/Player/DrDoom/RapidFireMagicGainComboEffect.prototype
        {   4176u, "Wolverine" },  // Powers/Player/Wolverine/ReviveCooldownDisplay.prototype
        {   4180u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Talents/Talent4RuinousFlux.prototype
        {   4183u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateSummonXmenBeast.prototype
        {   4184u, "Elektra" },  // Powers/Player/Elektra/KnifeRopeChainHotspotEffect.prototype
        {   4187u, "Nightcrawler" },  // Powers/Player/Nightcrawler/NewUltimate.prototype
        {   4188u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveDocOcProcEffect.prototype
        {   4189u, "Black Widow" },  // Powers/Player/BlackWidow/KnifeBuffCombo.prototype
        {   4192u, "Beast" },  // Powers/Player/Beast/Talents/Talent5SigMomentumGen.prototype
        {   4195u, "Nova" },  // Powers/Player/Nova/NoPulsarImplosion.prototype
        {   4199u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/MeleeSquirrelCone.prototype
        {   4205u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/ControlledMobProcRemoveNoPetBuffDisabler.prototype
        {   4209u, "Deadpool" },  // Powers/Player/Deadpool/Rework/ServerLag.prototype
        {   4213u, "Black Cat" },  // Powers/Player/BlackCat/Talents/TalentNineLivesRecharge.prototype
        {   4216u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/zzzDeprecated/IronFistChiBurstEffect.prototype
        {   4220u, "Gambit" },  // Powers/Player/Gambit/Talents/Talent2CheatDeath.prototype
        {   4222u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ShadowBoltMissileEffect.prototype
        {   4224u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerCableViperBeam.prototype
        {   4231u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateProcs.prototype
        {   4233u, "Iron Man" },  // Powers/Player/IronMan/DeathFromAboveComboEffect.prototype
        {   4238u, "Ultron" },  // Powers/Player/Ultron/HomingMissilesMissileEffect.prototype
        {   4239u, "Black Panther" },  // Powers/Player/BlackPanther/Talents/PredatorsFrenzyBuffTalent.prototype
        {   4240u, "Luke Cage" },  // Powers/Player/LukeCage/ChargeSprint.prototype
        {   4243u, "Thing" },  // Powers/Player/Thing/Rework/CallHotheadAnimatedActor.prototype
        {   4244u, "Hawkeye" },  // Powers/Player/Hawkeye/ShriekingArrowTrickArrowCondition.prototype
        {   4248u, "Hulk" },  // Powers/Player/Hulk/Rework/ShockwaveSlowComboEffect.prototype
        {   4250u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Ultimate.prototype
        {   4253u, "Black Widow" },  // Powers/Player/BlackWidow/PlastiqueRemoteDetonatorTimer.prototype
        {   4254u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateBeastMelee2.prototype
        {   4255u, "Thor" },  // Powers/Player/Thor/Rework/UltimateGodBlastBuffCombo.prototype
        {   4258u, "Ultron" },  // Powers/Player/Ultron/LeapStrike.prototype
        {   4259u, "Vision" },  // Powers/Player/Vision/SolarConeAllDamageBonus.prototype
        {   4261u, "Psylocke" },  // Powers/Player/Psylocke/DashBackstabCombo.prototype
        {   4263u, "War Machine" },  // Powers/Player/WarMachine/LaserBladeDash.prototype
        {   4266u, "Juggernaut" },  // Powers/Player/Juggernaut/TriplePunch.prototype
        {   4268u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/GunTurretNukeMissileEffect.prototype
        {   4269u, "Loki" },  // Powers/Player/Loki/UltimateChargeComboSummon.prototype
        {   4270u, "Rogue" },  // Powers/Player/Rogue/RecallOverloadEndExplosionMental.prototype
        {   4271u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveWarMachine.prototype
        {   4272u, "Gambit" },  // Powers/Player/Gambit/Talents/Talent2SleightOfHand.prototype
        {   4273u, "Human Torch" },  // Powers/Player/HumanTorch/SummonFireHotspotEffect.prototype
        {   4276u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ObfuscationChaosEnergyRestore.prototype
        {   4279u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/GoForTheEyesHit2.prototype
        {   4282u, "Angela" },  // Powers/Player/Angela/ExecuteCombo.prototype
        {   4286u, "Venom" },  // Powers/Player/Venom/Traits/DefenseTrait.prototype
        {   4287u, "She-Hulk" },  // Powers/Player/SheHulk/ClosingArguments.prototype
        {   4291u, "Captain America" },  // Powers/Player/CaptainAmerica/Vault.prototype
        {   4292u, "Angela" },  // Powers/Player/Angela/DisablingRibbonsBleed.prototype
        {   4296u, "Nova" },  // Powers/Player/Nova/HeavyBlast.prototype
        {   4298u, "Nick Fury" },  // Powers/Player/NickFury/MinigunAgentAttackHotspotEffect.prototype
        {   4299u, "Gambit" },  // Powers/Player/Gambit/AceOfSpadesDeathEffect.prototype
        {   4300u, "Iceman" },  // Powers/Player/Iceman/BasicBeam.prototype
        {   4302u, "Black Cat" },  // Powers/Player/BlackCat/TrapSignatureTrapComboExplosion.prototype
        {   4303u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SuperSkrullWhirlwindFireHotspotEffect.prototype
        {   4304u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshuStatueSpiritOnHit.prototype
        {   4307u, "Cable" },  // Powers/Player/Cable/PsimitarWavesOuterDamageCombo.prototype
        {   4308u, "She-Hulk" },  // Powers/Player/SheHulk/HighlightComboFinishers.prototype
        {   4310u, "Black Panther" },  // Powers/Player/BlackPanther/UltimatePanthernadoEffect.prototype
        {   4311u, "Human Torch" },  // Powers/Player/HumanTorch/SummonFireWallWeakenEffect.prototype
        {   4312u, "She-Hulk" },  // Powers/Player/SheHulk/Traits/MechanicTraitAnger.prototype
        {   4315u, "Magik" },  // Powers/Player/Magik/Assassinate.prototype
        {   4317u, "Magneto" },  // Powers/Player/Magneto/MetalGainOverTime.prototype
        {   4318u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceGainComboAll.prototype
        {   4319u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassivePyro.prototype
        {   4320u, "Hulk" },  // Powers/Player/Hulk/Rework/LeapQuakeVulnCombo.prototype
        {   4323u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/ForcePushPhoenix.prototype
        {   4326u, "Black Bolt" },  // Powers/Player/BlackBolt/BasicPunch.prototype
        {   4327u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Talents/Talent1Support.prototype
        {   4328u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveEmmaFrost.prototype
        {   4330u, "Elektra" },  // Powers/Player/Elektra/Talents/NinjaMysticAlly.prototype
        {   4331u, "Thing" },  // Powers/Player/Thing/Rework/GroundSmash.prototype
        {   4333u, "Carnage" },  // Powers/Player/Carnage/BasicClawsBladeStaffSecondHit3.prototype
        {   4334u, "Magik" },  // Powers/Player/Magik/NastirhDiveBombEnd.prototype
        {   4336u, "Colossus" },  // Powers/Player/Colossus/WolverineSummon/DefaultAttack.prototype
        {   4337u, "Iron Fist" },  // Powers/Player/IronFist/SnakeStance.prototype
        {   4339u, "Elektra" },  // Powers/Player/Elektra/UltimateHotspotEffect.prototype
        {   4341u, "War Machine" },  // Powers/Player/WarMachine/Talents/Talent2ThermalExpulsion.prototype
        {   4342u, "Ultron" },  // Powers/Player/Ultron/MeleeDroneUppercut.prototype
        {   4343u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/MistyKnight/IceBeamHotspotEffect.prototype
        {   4344u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerLadyDeadpoolMeleeAttack3.prototype
        {   4346u, "Black Widow" },  // Powers/Player/BlackWidow/DodgeMartialArtsBuffProcEffect.prototype
        {   4348u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LimboDemonBossTeleport.prototype
        {   4349u, "Iceman" },  // Powers/Player/Iceman/FrostNova.prototype
        {   4350u, "X-23" },  // Powers/Player/X23/Talents/Talent2GWDurationCritChance.prototype
        {   4351u, "Magik" },  // Powers/Player/Magik/DarkPactNastirhBuff.prototype
        {   4359u, "Gambit" },  // Powers/Player/Gambit/BoBeatdown.prototype
        {   4360u, "Moon Knight" },  // Powers/Player/MoonKnight/CrescentDartFanDamage.prototype
        {   4361u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/Pummel5thAttack.prototype
        {   4362u, "Venom" },  // Powers/Player/Venom/DoubleSlash.prototype
        {   4366u, "Iron Man" },  // Powers/Player/IronMan/DeathFromAboveMeleeDamageIncrease.prototype
        {   4367u, "Gambit" },  // Powers/Player/Gambit/JacksOrBetterMissileEffect.prototype
        {   4368u, "War Machine" },  // Powers/Player/WarMachine/Talents/Talent3MachineGuns.prototype
        {   4369u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Talents/Talent3BadLuck.prototype
        {   4371u, "Black Panther" },  // Powers/Player/BlackPanther/EnergyDaggers.prototype
        {   4375u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaoticDebuffHiddenPassive.prototype
        {   4376u, "Carnage" },  // Powers/Player/Carnage/AxeBleedHiddenPassiveProcEffect.prototype
        {   4377u, "Colossus" },  // Powers/Player/Colossus/FastballSpecialCritAuraEffect.prototype
        {   4381u, "Elektra" },  // Powers/Player/Elektra/SpinningStrikeBleedUnmarked.prototype
        {   4382u, "Carnage" },  // Powers/Player/Carnage/Talents/AxeWeaponsDFA.prototype
        {   4385u, "Black Panther" },  // Powers/Player/BlackPanther/DoublePunchAttackSpeedBuff.prototype
        {   4387u, "Carnage" },  // Powers/Player/Carnage/BasicClaws.prototype
        {   4388u, "X-23" },  // Powers/Player/X23/SelfRez.prototype
        {   4389u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/MinigunMissileEffect.prototype
        {   4391u, "Magik" },  // Powers/Player/Magik/NastirhDiveBomb.prototype
        {   4392u, "Carnage" },  // Powers/Player/Carnage/Talents/MaceWeaponsMacePummel.prototype
        {   4396u, "Venom" },  // Powers/Player/Venom/IchorSpear.prototype
        {   4402u, "Iceman" },  // Powers/Player/Iceman/Talents/ChilledDoT.prototype
        {   4403u, "War Machine" },  // Powers/Player/WarMachine/WarMachineArmorCloakingDeviceStealthCombo.prototype
        {   4406u, "Winter Soldier" },  // Powers/Player/WinterSoldier/GunStanceTree1DamageBuff.prototype
        {   4410u, "Blade" },  // Powers/Player/Blade/Traits/AmmoRegen.prototype
        {   4415u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveRedSkullProcEffectSupport.prototype
        {   4419u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveMadameHydra.prototype
        {   4421u, "Loki" },  // Powers/Player/Loki/Talents/FourRealmsSoulCrush.prototype
        {   4422u, "Human Torch" },  // Powers/Player/HumanTorch/FlameWaveMissileEffect.prototype
        {   4423u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveKingpin.prototype
        {   4429u, "Venom" },  // Powers/Player/Venom/SymbioteDrainPowerRed1.prototype
        {   4430u, "Cyclops" },  // Powers/Player/Cyclops/Rework/AoEBeam.prototype
        {   4435u, "Doctor Strange" },  // Powers/Player/DoctorStrange/SeraphimShieldProtectedCombo.prototype
        {   4446u, "War Machine" },  // Powers/Player/WarMachine/RecurringMissileEffect.prototype
        {   4455u, "Human Torch" },  // Powers/Player/HumanTorch/BowlingBallMissileEffect.prototype
        {   4457u, "Loki" },  // Powers/Player/Loki/MindControl.prototype
        {   4458u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LukeCagePummel3rdAttack.prototype
        {   4459u, "Loki" },  // Powers/Player/Loki/SearingEmbersTrigger.prototype
        {   4460u, "Deadpool" },  // Powers/Player/Deadpool/Rework/SuperiorHealingFactor.prototype
        {   4463u, "Punisher" },  // Powers/Player/Punisher/Rework/PassiveToughCleanseCCImmuneCombo.prototype
        {   4467u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/PlasmaCannonLarger.prototype
        {   4468u, "Kitty Pryde" },  // Powers/Player/KittyPryde/UltimateSteroidEliteProcEffect.prototype
        {   4470u, "Daredevil" },  // Powers/Player/Daredevil/Update/OpenerStaminaRestoreProc.prototype
        {   4472u, "Ghost Rider" },  // Powers/Player/GhostRider/BikeLunge.prototype
        {   4474u, "Silver Surfer" },  // Powers/Player/SilverSurfer/SingularityAsProc.prototype
        {   4477u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/MomentumAlwaysGenerates.prototype
        {   4478u, "Thor" },  // Powers/Player/Thor/Rework/RagnarokInnerCombo.prototype
        {   4486u, "Vision" },  // Powers/Player/Vision/PhaseModeProcEffect.prototype
        {   4487u, "Hulk" },  // Powers/Player/Hulk/Rework/LeapQuakeEndVeryAngry.prototype
        {   4488u, "Human Torch" },  // Powers/Player/HumanTorch/Traits/OverheatSummonHotspots.prototype
        {   4490u, "Gambit" },  // Powers/Player/Gambit/UltimateRogueDefaultAttackCombo2.prototype
        {   4493u, "Angela" },  // Powers/Player/Angela/UltRibbonPull.prototype
        {   4497u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/BasicRifleMissileEffect.prototype
        {   4498u, "Magik" },  // Powers/Player/Magik/BoneShieldThornsProc.prototype
        {   4500u, "War Machine" },  // Powers/Player/WarMachine/HeatGainAutoGunMissiles.prototype
        {   4501u, "Nova" },  // Powers/Player/Nova/ArcBurstPulsarKill.prototype
        {   4502u, "Taskmaster" },  // Powers/Player/Taskmaster/FreezeArrowMissileEffect.prototype
        {   4503u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/ControlledMobAllianceOverride.prototype
        {   4509u, "Iron Fist" },  // Powers/Player/IronFist/LeopardSlashAoEProcEffectPummel.prototype
        {   4510u, "Captain America" },  // Powers/Player/CaptainAmerica/OnBlockReduceCooldownHiddenPass.prototype
        {   4511u, "Kitty Pryde" },  // Powers/Player/KittyPryde/UltimateSteroidPopcornProcEffect.prototype
        {   4512u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/UltimateNoMoreHotspotEffect.prototype
        {   4513u, "Thor" },  // Powers/Player/Thor/Rework/ThunderSpot.prototype
        {   4514u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/SignatureNuke.prototype
        {   4517u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/BasicCrescentExplosiveRapidFireBounce.prototype
        {   4519u, "Wolverine" },  // Powers/Player/Wolverine/LungeSprint.prototype
        {   4520u, "Black Cat" },  // Powers/Player/BlackCat/NineLivesGain.prototype
        {   4525u, "Cyclops" },  // Powers/Player/Cyclops/Rework/BasicPunchReworkCombo.prototype
        {   4530u, "Captain America" },  // Powers/Player/CaptainAmerica/FinestHourEnduranceBuff.prototype
        {   4532u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/IronFistTechniqueDragonStanceBuff.prototype
        {   4533u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveHydraAgent.prototype
        {   4534u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BoardSweepEndRemoveStacks.prototype
        {   4535u, "Daredevil" },  // Powers/Player/Daredevil/Update/WhirlingClub.prototype
        {   4536u, "Storm" },  // Powers/Player/Storm/BallLightning.prototype
        {   4538u, "Human Torch" },  // Powers/Player/HumanTorch/ConsumeBuffEffectFixedDuration.prototype
        {   4540u, "Silver Surfer" },  // Powers/Player/SilverSurfer/TeleportDashClearStacks.prototype
        {   4541u, "Wolverine" },  // Powers/Player/Wolverine/FlyingBleedDoT.prototype
        {   4544u, "Luke Cage" },  // Powers/Player/LukeCage/PummelNoFinisher6thAttack.prototype
        {   4545u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/FireAndIce.prototype
        {   4547u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/HerbieHealAura.prototype
        {   4550u, "Elektra" },  // Powers/Player/Elektra/SpinningStrikeEnd.prototype
        {   4551u, "Luke Cage" },  // Powers/Player/LukeCage/UltimateBoulderBeatdown.prototype
        {   4552u, "Jean Grey" },  // Powers/Player/JeanGrey/UltimateHotspotSummonComboPhx.prototype
        {   4560u, "Human Torch" },  // Powers/Player/HumanTorch/FlameTornado.prototype
        {   4561u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerCablePulseBoltMissileEffect.prototype
        {   4565u, "Rogue" },  // Powers/Player/Rogue/UltimateSignatureBamf.prototype
        {   4567u, "Colossus" },  // Powers/Player/Colossus/CallKittySummonCombo.prototype
        {   4575u, "X-23" },  // Powers/Player/X23/FuriousLunge.prototype
        {   4583u, "Cyclops" },  // Powers/Player/Cyclops/Traits/DefenseTrait.prototype
        {   4584u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/RobbieReyesDriveByHotspots.prototype
        {   4586u, "Nick Fury" },  // Powers/Player/NickFury/WarMachinePlasmaCannonHotspotEffect.prototype
        {   4588u, "Silver Surfer" },  // Powers/Player/SilverSurfer/TimeWarpCooldownDisplay.prototype
        {   4589u, "Beast" },  // Powers/Player/Beast/SleepGasGadgetShockCloudSummon.prototype
        {   4590u, "Gambit" },  // Powers/Player/Gambit/EnhancedCost100.prototype
        {   4595u, "Deadpool" },  // Powers/Player/Deadpool/Rework/LilDeadpoolDollDeathEffect.prototype
        {   4599u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BasicPunch.prototype
        {   4602u, "Captain America" },  // Powers/Player/CaptainAmerica/SerumHiddenPassive.prototype
        {   4603u, "War Machine" },  // Powers/Player/WarMachine/WarMachineArmorWeakenCombo.prototype
        {   4605u, "Doctor Strange" },  // Powers/Player/DoctorStrange/ConeShards.prototype
        {   4608u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/KineticBoltJeanEffect.prototype
        {   4609u, "Magik" },  // Powers/Player/Magik/BoneWall.prototype
        {   4610u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseToggleHiddenPassive.prototype
        {   4611u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/StealthToggleOutOfCombatCombo.prototype
        {   4613u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent4DancingKatana.prototype
        {   4614u, "Emma Frost" },  // Powers/Player/EmmaFrost/UltimateSpiderRappelMovement.prototype
        {   4616u, "Ghost Rider" },  // Powers/Player/GhostRider/NewUltimate.prototype
        {   4621u, "Hulk" },  // Powers/Player/Hulk/Rework/LeapQuake.prototype
        {   4622u, "Thor" },  // Powers/Player/Thor/MassiveLightningStrikeCombo.prototype
        {   4625u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/NightcrawlerValiantLeap.prototype
        {   4626u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/SpiritBolt.prototype
        {   4631u, "She-Hulk" },  // Powers/Player/SheHulk/UltimatePillarHotspotEffect.prototype
        {   4632u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/IronFistCraneStanceUppercutCharge.prototype
        {   4635u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ExpandingPBAoEMissileCombo.prototype
        {   4637u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BasicSwordSlash.prototype
        {   4639u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LukeCagePummel5thAttack.prototype
        {   4640u, "Black Widow" },  // Powers/Player/BlackWidow/Traits/ChargeRegenEnd.prototype
        {   4643u, "Dr. Doom" },  // Powers/Player/DrDoom/Talents/Talent2EldritchReckoning.prototype
        {   4646u, "Deadpool" },  // Powers/Player/Deadpool/Rework/BangBang.prototype
        {   4647u, "Nova" },  // Powers/Player/Nova/Talents/Talent4PulsarFreeSecond.prototype
        {   4650u, "Carnage" },  // Powers/Player/Carnage/UltimateBuffComboEffect.prototype
        {   4651u, "Beast" },  // Powers/Player/Beast/HulkingSlamJubileeBeastHit.prototype
        {   4657u, "Colossus" },  // Powers/Player/Colossus/InCombatArmorBuffEnd.prototype
        {   4659u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondHeartMentalCombo.prototype
        {   4666u, "Juggernaut" },  // Powers/Player/Juggernaut/MomentumDecayAsComboRemovalProc.prototype
        {   4670u, "Angela" },  // Powers/Player/Angela/DisablingRibbons.prototype
        {   4676u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/TalentKineticBatteryRegen.prototype
        {   4679u, "Nova" },  // Powers/Player/Nova/PulsarExplosionProc.prototype
        {   4685u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/ReconstructDeconstruct.prototype
        {   4687u, "Black Bolt" },  // Powers/Player/BlackBolt/MasterBlowSignatureStart.prototype
        {   4688u, "Magik" },  // Powers/Player/Magik/BFLDLeapAttackEnd.prototype
        {   4689u, "Jean Grey" },  // Powers/Player/JeanGrey/PhoenixFormFromHybridSpec.prototype
        {   4692u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HavokConeShotEnergyDamageBuffEffect.prototype
        {   4694u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateBattleVanKnockback.prototype
        {   4696u, "Angela" },  // Powers/Player/Angela/SigNoMatchFinalHitTimer.prototype
        {   4697u, "Iron Fist" },  // Powers/Player/IronFist/CraneStanceVisual.prototype
        {   4700u, "Ghost Rider" },  // Powers/Player/GhostRider/BasicFireballEnduranceGainCombo.prototype
        {   4703u, "Deadpool" },  // Powers/Player/Deadpool/Talents/StrafeSlamExplosionsTalent.prototype
        {   4704u, "Carnage" },  // Powers/Player/Carnage/Talents/MaceWeaponsCharges.prototype
        {   4706u, "Daredevil" },  // Powers/Player/Daredevil/Talents/SigBuffTalent.prototype
        {   4708u, "Vision" },  // Powers/Player/Vision/Talents/Talent1SolarConeBuffMeleeDmg.prototype
        {   4710u, "Loki" },  // Powers/Player/Loki/LokiIllusionMeleeAttack3.prototype
        {   4713u, "X-23" },  // Powers/Player/X23/FerocityStackHighlightFinishers.prototype
        {   4714u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceImbuedBuffLong.prototype
        {   4715u, "Deadpool" },  // Powers/Player/Deadpool/Talents/SmellsLikeVictoryTalent.prototype
        {   4716u, "Nick Fury" },  // Powers/Player/NickFury/BasicPistolMissileEffect.prototype
        {   4717u, "Taskmaster" },  // Powers/Player/Taskmaster/ThreeRoundBurstMissileEffect.prototype
        {   4718u, "Iceman" },  // Powers/Player/Iceman/UltimateCloneOnDeathExplosion.prototype
        {   4726u, "Loki" },  // Powers/Player/Loki/EternalDarkness.prototype
        {   4727u, "Winter Soldier" },  // Powers/Player/WinterSoldier/KnifeThrowStunEffect.prototype
        {   4729u, "Dr. Doom" },  // Powers/Player/DrDoom/MagicResourceHiddenPassive.prototype
        {   4730u, "Thor" },  // Powers/Player/Thor/Talents/DeathFromAboveTalent.prototype
        {   4735u, "War Machine" },  // Powers/Player/WarMachine/UltimateSidekickComboKnockdown.prototype
        {   4736u, "Iceman" },  // Powers/Player/Iceman/FuriousLunge.prototype
        {   4739u, "Thing" },  // Powers/Player/Thing/Talents/Talent4BrawlingBoost.prototype
        {   4741u, "Luke Cage" },  // Powers/Player/LukeCage/Talents/ComboPointsComboFighter.prototype
        {   4744u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PullUnderDamageShieldCombo.prototype
        {   4750u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/HulkbusterSquirrels.prototype
        {   4756u, "Moon Knight" },  // Powers/Player/MoonKnight/RicochetBonusDamage.prototype
        {   4757u, "Kitty Pryde" },  // Powers/Player/KittyPryde/UltimateMissileEffect.prototype
        {   4758u, "Luke Cage" },  // Powers/Player/LukeCage/StreetKick.prototype
        {   4760u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/MeleeSquirrelConeExtraSquirrels.prototype
        {   4761u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DamageMaelstromStun.prototype
        {   4762u, "War Machine" },  // Powers/Player/WarMachine/SidekickHotspotEffect.prototype
        {   4763u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/SpikedBallChanneledSummonCombo.prototype
        {   4764u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/JuggernautImInvulnerableCDisplay.prototype
        {   4769u, "Magik" },  // Powers/Player/Magik/LifeTapVulnCombo.prototype
        {   4770u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ImplosionGadgetTrigger.prototype
        {   4776u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BasicBeamMoreTargets.prototype
        {   4780u, "Iron Fist" },  // Powers/Player/IronFist/DragonStanceSingleStanceBuff.prototype
        {   4784u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCardStage3.prototype
        {   4785u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/TeslaCoilGadget.prototype
        {   4788u, "Kitty Pryde" },  // Powers/Player/KittyPryde/SwordPBAoEBleed.prototype
        {   4792u, "Vision" },  // Powers/Player/Vision/ModeToggleSwitchToDensePunchBonus.prototype
        {   4793u, "Black Widow" },  // Powers/Player/BlackWidow/MicrodronesRandomPosition.prototype
        {   4795u, "Colossus" },  // Powers/Player/Colossus/MovementSpinUpgraded.prototype
        {   4802u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GrootRideGrootHotspotEffect.prototype
        {   4805u, "Psylocke" },  // Powers/Player/Psylocke/Traits/MechanicTraitPsiBarrier.prototype
        {   4806u, "Black Cat" },  // Powers/Player/BlackCat/Talents/TalentSignatureThrowTraps.prototype
        {   4809u, "Black Cat" },  // Powers/Player/BlackCat/TrapSignature.prototype
        {   4810u, "Gambit" },  // Powers/Player/Gambit/Talents/Talent3ThreeOfAKind.prototype
        {   4812u, "Rogue" },  // Powers/Player/Rogue/Traits/StolenPassivePowerSlot1.prototype
        {   4816u, "Magik" },  // Powers/Player/Magik/SoulswordWideSlash.prototype
        {   4818u, "Black Widow" },  // Powers/Player/BlackWidow/MicrodronesRandomPositionSlowerHit.prototype
        {   4826u, "Ghost Rider" },  // Powers/Player/GhostRider/LoopChainWhirlwindMovingDamageArea.prototype
        {   4828u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/Pummel3rdAttack.prototype
        {   4831u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/SpeedRushJeanStartCombo.prototype
        {   4834u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MandarinElectricStormHotspotEffe.prototype
        {   4836u, "Iron Fist" },  // Powers/Player/IronFist/FiveStanceMasteryStack.prototype
        {   4839u, "Carnage" },  // Powers/Player/Carnage/ExcessTalentsBuffEffect.prototype
        {   4840u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/DiamondArmorAlwaysOn.prototype
        {   4841u, "Nightcrawler" },  // Powers/Player/Nightcrawler/SwordPummel4thAttack.prototype
        {   4844u, "Thing" },  // Powers/Player/Thing/Rework/DiscusTossTalented.prototype
        {   4847u, "Gambit" },  // Powers/Player/Gambit/Talents/Talent2KingOfHearts.prototype
        {   4848u, "Ghost Rider" },  // Powers/Player/GhostRider/ChainSpecActiveBasicFireballDoTStack.prototype
        {   4849u, "Beast" },  // Powers/Player/Beast/Talents/Talent2PummelCooldownReset.prototype
        {   4855u, "Black Panther" },  // Powers/Player/BlackPanther/MineFieldRanged.prototype
        {   4858u, "Cyclops" },  // Powers/Player/Cyclops/TeamSteroidInvulnCombo.prototype
        {   4864u, "Black Panther" },  // Powers/Player/BlackPanther/TripleShot.prototype
        {   4865u, "Angela" },  // Powers/Player/Angela/SwordPummel1stAttack.prototype
        {   4866u, "Moon Knight" },  // Powers/Player/MoonKnight/HighlightSteroidsProcEffect.prototype
        {   4867u, "Iron Fist" },  // Powers/Player/IronFist/FlyingKickEffect.prototype
        {   4875u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/StealthToggleConditionRemovalB.prototype
        {   4876u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallIcemanProc.prototype
        {   4878u, "Vision" },  // Powers/Player/Vision/EnhanceRobotSummonRemoval.prototype
        {   4879u, "Rogue" },  // Powers/Player/Rogue/RapidPunchDashCDR.prototype
        {   4883u, "Storm" },  // Powers/Player/Storm/StormSurgeLightningTempest.prototype
        {   4884u, "Black Cat" },  // Powers/Player/BlackCat/ClawPummelClawDamageBonusCombo.prototype
        {   4888u, "Ant-Man" },  // Powers/Player/AntMan/GiantManFootDamageCombo.prototype
        {   4889u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent3DeathFromAboveArmorSpend.prototype
        {   4890u, "Iron Man" },  // Powers/Player/IronMan/SignatureSweepLeft.prototype
        {   4892u, "Gambit" },  // Powers/Player/Gambit/BatterUpStunCombo.prototype
        {   4896u, "Doctor Strange" },  // Powers/Player/DoctorStrange/MysticEnergyDelayBeforeRemoval.prototype
        {   4900u, "Punisher" },  // Powers/Player/Punisher/Talents/SMG.prototype
        {   4901u, "Cyclops" },  // Powers/Player/Cyclops/ConcussiveForceKBEffect.prototype
        {   4905u, "Cable" },  // Powers/Player/Cable/PsychicHazeDoTCombo.prototype
        {   4906u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ElectroElementalStorm.prototype
        {   4907u, "Iceman" },  // Powers/Player/Iceman/IceBlockRevive.prototype
        {   4908u, "Nova" },  // Powers/Player/Nova/Talents/Talent5MaxShieldDFACharge.prototype
        {   4913u, "Iron Fist" },  // Powers/Player/IronFist/Talents/Talent1OpenerCDR.prototype
        {   4915u, "War Machine" },  // Powers/Player/WarMachine/FlameThrowerHotSpotEffect.prototype
        {   4917u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelCounterDown.prototype
        {   4918u, "Captain America" },  // Powers/Player/CaptainAmerica/SerumHasPipsShieldThrowRemoval.prototype
        {   4919u, "Loki" },  // Powers/Player/Loki/MainSpecRangedBuffLight.prototype
        {   4920u, "Punisher" },  // Powers/Player/Punisher/Rework/Bazooka.prototype
        {   4922u, "Psylocke" },  // Powers/Player/Psylocke/Traits/BarrierAbsorbStopperEnd.prototype
        {   4925u, "Cable" },  // Powers/Player/Cable/PsimitarWavesKeywordConditionCombo.prototype
        {   4929u, "Ghost Rider" },  // Powers/Player/GhostRider/FireBreathBasicFireballDoT1stTic.prototype
        {   4932u, "War Machine" },  // Powers/Player/WarMachine/ChaingunBurstMissileEffect.prototype
        {   4936u, "Iceman" },  // Powers/Player/Iceman/IceGolemSummonComboThreeGolems.prototype
        {   4941u, "Angela" },  // Powers/Player/Angela/WhippingRibbonsRecurringEffect.prototype
        {   4942u, "Winter Soldier" },  // Powers/Player/WinterSoldier/TripleShotMissileEffect2.prototype
        {   4945u, "Venom" },  // Powers/Player/Venom/Talents/DefenseBuff.prototype
        {   4946u, "Moon Knight" },  // Powers/Player/MoonKnight/Strafe.prototype
        {   4948u, "Iceman" },  // Powers/Player/Iceman/DeepFreezeFilterPowerAbsoluteZero.prototype
        {   4949u, "Thor" },  // Powers/Player/Thor/Rework/HammerDashStunCombo.prototype
        {   4950u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/StretchyBrainDeactivateGiantGun.prototype
        {   4951u, "Luke Cage" },  // Powers/Player/LukeCage/GoodAtCombosTooltipDriver.prototype
        {   4952u, "Ghost Rider" },  // Powers/Player/GhostRider/Talents/Talent4LoopChainBike.prototype
        {   4953u, "Magneto" },  // Powers/Player/Magneto/LungeCollisionEffect.prototype
        {   4956u, "Colossus" },  // Powers/Player/Colossus/WolverineSummon/FlyingClaws.prototype
        {   4958u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/PBAoEChargeFullSpend.prototype
        {   4959u, "Cyclops" },  // Powers/Player/Cyclops/Talents/BeamRefractionTalent.prototype
        {   4961u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerHydraBobMissileEffect.prototype
        {   4963u, "Nova" },  // Powers/Player/Nova/ArcBurstMegaPunchDamageBuff.prototype
        {   4965u, "Nightcrawler" },  // Powers/Player/Nightcrawler/SwordPummel.prototype
        {   4968u, "Rogue" },  // Powers/Player/Rogue/Taunt.prototype
        {   4971u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/CaptainsVigorTalent.prototype
        {   4973u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ElektraShadowStrikeReappear.prototype
        {   4974u, "Iron Man" },  // Powers/Player/IronMan/UltimateSuitMicromissiles.prototype
        {   4975u, "X-23" },  // Powers/Player/X23/TripleKickCDR.prototype
        {   4977u, "Thor" },  // Powers/Player/Thor/Rework/ShockwaveHotspotEffect.prototype
        {   4979u, "Loki" },  // Powers/Player/Loki/UltimateTransformComboDeactivate.prototype
        {   4980u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/BlackHoleSurferBuff.prototype
        {   4982u, "Taskmaster" },  // Powers/Player/Taskmaster/UltimateHawkeyeTurretArrow.prototype
        {   4986u, "Loki" },  // Powers/Player/Loki/EternalFlameHotspotEffect.prototype
        {   4987u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ImplodeExplodeShieldCombo.prototype
        {   4992u, "Emma Frost" },  // Powers/Player/EmmaFrost/Ultimate.prototype
        {   4993u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent4GammaPunchBonus.prototype
        {   4995u, "Magneto" },  // Powers/Player/Magneto/AllInImplosionCombo.prototype
        {   4996u, "Colossus" },  // Powers/Player/Colossus/GroupTauntTalentBuff.prototype
        {   5005u, "Hawkeye" },  // Powers/Player/Hawkeye/TenArrowSpeedLoaderMissileEffect.prototype
        {   5009u, "Black Widow" },  // Powers/Player/BlackWidow/CleanseMedkitHealingProc.prototype
        {   5014u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Talents/Talent5AlterRealityBuff.prototype
        {   5015u, "Ghost Rider" },  // Powers/Player/GhostRider/HellfireSpecActiveExplodeEffect.prototype
        {   5017u, "Wolverine" },  // Powers/Player/Wolverine/Talents/Talent4BasicBleedVuln.prototype
        {   5027u, "Iceman" },  // Powers/Player/Iceman/ShatterBuffCombo.prototype
        {   5028u, "Black Panther" },  // Powers/Player/BlackPanther/Talents/NoDoraTalent.prototype
        {   5030u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ForcePillarSummonCombo.prototype
        {   5032u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCardProcMissilePower.prototype
        {   5033u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DamageConeHotspotIntervalEffect.prototype
        {   5036u, "Carnage" },  // Powers/Player/Carnage/Talents/ClawWeaponsExcessHealingStorage.prototype
        {   5039u, "Black Widow" },  // Powers/Player/BlackWidow/FlipKickMineOnDeath.prototype
        {   5040u, "Cyclops" },  // Powers/Player/Cyclops/Rework/BasicPiercingMissileEffect.prototype
        {   5042u, "Cyclops" },  // Powers/Player/Cyclops/Rework/BasicBouncingBeam.prototype
        {   5046u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AxeHeelDropEnd.prototype
        {   5047u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCardMissileEffectStage4.prototype
        {   5049u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveWinterSoldierProcEffect.prototype
        {   5050u, "Magik" },  // Powers/Player/Magik/Ultimate.prototype
        {   5057u, "Captain America" },  // Powers/Player/CaptainAmerica/Talents/HeroicStrikeShieldSwipeSpec.prototype
        {   5060u, "Hawkeye" },  // Powers/Player/Hawkeye/DisengagingShotMissileEffect.prototype
        {   5062u, "Psylocke" },  // Powers/Player/Psylocke/PassiveDecoys.prototype
        {   5065u, "Nick Fury" },  // Powers/Player/NickFury/ChanneledBeam.prototype
        {   5069u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/KhonshuStatueTerrify.prototype
        {   5070u, "Winter Soldier" },  // Powers/Player/WinterSoldier/OnKillStealthHiddenPassive.prototype
        {   5072u, "Hawkeye" },  // Powers/Player/Hawkeye/Traits/OffenseTrait.prototype
        {   5073u, "Angela" },  // Powers/Player/Angela/HevensWrathBuffRemoval.prototype
        {   5074u, "Daredevil" },  // Powers/Player/Daredevil/Talents/SigCooldownReductionTalent.prototype
        {   5082u, "Luke Cage" },  // Powers/Player/LukeCage/SweetChristmasShockwaveCombo.prototype
        {   5083u, "War Machine" },  // Powers/Player/WarMachine/TearGas.prototype
        {   5085u, "Loki" },  // Powers/Player/Loki/EternalFlame.prototype
        {   5089u, "Nick Fury" },  // Powers/Player/NickFury/BasicPistolCombo2.prototype
        {   5090u, "Angela" },  // Powers/Player/Angela/UltimateHaltMovement.prototype
        {   5092u, "Taskmaster" },  // Powers/Player/Taskmaster/ComboPointConsume.prototype
        {   5097u, "Black Bolt" },  // Powers/Player/BlackBolt/EnergyGain50PctCombo.prototype
        {   5109u, "Carnage" },  // Powers/Player/Carnage/MacePummelDamageComboRight.prototype
        {   5119u, "Ultron" },  // Powers/Player/Ultron/Talents/Talent2LeapstrikeCharges.prototype
        {   5120u, "Juggernaut" },  // Powers/Player/Juggernaut/WrathOfCyttorak.prototype
        {   5122u, "Iceman" },  // Powers/Player/Iceman/IceGolemSnowballRight.prototype
        {   5125u, "Storm" },  // Powers/Player/Storm/Talents/WindTempest.prototype
        {   5126u, "Green Goblin" },  // Powers/Player/GreenGoblin/IncendiaryPumpkin.prototype
        {   5127u, "Moon Knight" },  // Powers/Player/MoonKnight/StrafeMissileEffect.prototype
        {   5129u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SauronSwoopingFlamesHotspotEffec.prototype
        {   5132u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/ControlMobNoPetBuff.prototype
        {   5133u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseDashRemoveHidemesh.prototype
        {   5134u, "Vision" },  // Powers/Player/Vision/Talents/Talent2DensityShiftCDRDefBuff.prototype
        {   5138u, "Cable" },  // Powers/Player/Cable/TKOverloadKnockup.prototype
        {   5140u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent1RegenArmorProcEffect.prototype
        {   5141u, "Venom" },  // Powers/Player/Venom/FuriousLungeKnockdownEffect.prototype
        {   5142u, "Venom" },  // Powers/Player/Venom/BigWebShoot.prototype
        {   5145u, "Captain America" },  // Powers/Player/CaptainAmerica/VibraniumMechanic.prototype
        {   5147u, "Doctor Strange" },  // Powers/Player/DoctorStrange/MysticEnergyOrbVisual2.prototype
        {   5151u, "Nick Fury" },  // Powers/Player/NickFury/Talents/Talent5SigCooldownResets.prototype
        {   5153u, "Juggernaut" },  // Powers/Player/Juggernaut/AvatarOfCyttorak.prototype
        {   5155u, "Iceman" },  // Powers/Player/Iceman/Talents/ChillFreezePotency.prototype
        {   5160u, "Dr. Doom" },  // Powers/Player/DrDoom/DoomsDay.prototype
        {   5161u, "Hawkeye" },  // Powers/Player/Hawkeye/AdamantiumArrow.prototype
        {   5162u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveShocker.prototype
        {   5164u, "Beast" },  // Powers/Player/Beast/PreventMomentumDecay750MS.prototype
        {   5166u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CosmicWakeStartWaking.prototype
        {   5167u, "Nightcrawler" },  // Powers/Player/Nightcrawler/UltimateComboBuff.prototype
        {   5168u, "Magik" },  // Powers/Player/Magik/Talents/Talent1SoulConeLayer.prototype
        {   5174u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BamfHotspot.prototype
        {   5178u, "Psylocke" },  // Powers/Player/Psylocke/KatanaPBAoELimiter.prototype
        {   5179u, "X-23" },  // Powers/Player/X23/Talents/Talent2EvisMvmtSTSSDmg.prototype
        {   5186u, "Hawkeye" },  // Powers/Player/Hawkeye/DisengagingShotMedkitCombo.prototype
        {   5187u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BlobBellyFlopEnd.prototype
        {   5188u, "Venom" },  // Powers/Player/Venom/DefensePassiveIgnoreDeath.prototype
        {   5190u, "Angela" },  // Powers/Player/Angela/Traits/MechanicTraitWhippingRibbons.prototype
        {   5192u, "Nova" },  // Powers/Player/Nova/ArcBurstMegaPunchDamageBuff2.prototype
        {   5198u, "Nova" },  // Powers/Player/Nova/ChanneledPulsarInvulnRemover.prototype
        {   5201u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/DarkHexVulnToChaosBlast.prototype
        {   5202u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HydeDirectedShockwave.prototype
        {   5205u, "Dr. Doom" },  // Powers/Player/DrDoom/Talents/Talent4MagicSignature.prototype
        {   5207u, "Iron Man" },  // Powers/Player/IronMan/DeathFromAbove.prototype
        {   5209u, "Magneto" },  // Powers/Player/Magneto/ElectromagneticShockwaveMissileEffectStronger.prototype
        {   5210u, "Winter Soldier" },  // Powers/Player/WinterSoldier/StealthMineTossComboGrenade1.prototype
        {   5211u, "Iron Fist" },  // Powers/Player/IronFist/ChiPunch.prototype
        {   5212u, "Gambit" },  // Powers/Player/Gambit/RaininPain.prototype
        {   5213u, "Iron Fist" },  // Powers/Player/IronFist/IronFistPunchCraneHealing.prototype
        {   5216u, "Iceman" },  // Powers/Player/Iceman/Traits/ArmorRegenInCombatPause.prototype
        {   5219u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveMoonKnightHealthMinCondition.prototype
        {   5222u, "Iron Fist" },  // Powers/Player/IronFist/BlackBlackPoisonTouchEffect.prototype
        {   5223u, "Beast" },  // Powers/Player/Beast/StompOverlapProcEffect.prototype
        {   5225u, "Cyclops" },  // Powers/Player/Cyclops/FocusBeamBuffPhase2Start.prototype
        {   5231u, "Iron Man" },  // Powers/Player/IronMan/UltimateSuitRepulsorBurstEffect.prototype
        {   5234u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ConeYank.prototype
        {   5241u, "Magik" },  // Powers/Player/Magik/LimboDemonClawAttack.prototype
        {   5242u, "Daredevil" },  // Powers/Player/Daredevil/Update/BouncingStrikeChainPower.prototype
        {   5243u, "Ultron" },  // Powers/Player/Ultron/BladeDroneLunge.prototype
        {   5245u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshuSteroidHealthVisual2.prototype
        {   5253u, "War Machine" },  // Powers/Player/WarMachine/Talents/Talent5EnergyBarrier.prototype
        {   5258u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/AlterRealityKnockbackComboEffect.prototype
        {   5259u, "Nick Fury" },  // Powers/Player/NickFury/Traits/DefaultAmmoRegenEndCombo.prototype
        {   5260u, "Ultron" },  // Powers/Player/Ultron/Talents/Talent1EncephaloBeamBuff.prototype
        {   5261u, "Carnage" },  // Powers/Player/Carnage/ReapingTimeHotspotEffect.prototype
        {   5265u, "Deadpool" },  // Powers/Player/Deadpool/PowerUpHulkOutEffect.prototype
        {   5266u, "Luke Cage" },  // Powers/Player/LukeCage/Talents/MistyKnight.prototype
        {   5269u, "Thing" },  // Powers/Player/Thing/DirectedShockwaveMissileEffect.prototype
        {   5272u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/RitualCircle.prototype
        {   5277u, "Elektra" },  // Powers/Player/Elektra/KillCommandCombo.prototype
        {   5281u, "Black Widow" },  // Powers/Player/BlackWidow/TwilightInitiativeComboResource.prototype
        {   5284u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallBeastMelee3.prototype
        {   5286u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/DaredevilsMastery.prototype
        {   5290u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/ArcTurretTaserHotspotEffect.prototype
        {   5291u, "Juggernaut" },  // Powers/Player/Juggernaut/EarthquakeLeapEnd.prototype
        {   5292u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/GoForTheEyesHit7.prototype
        {   5293u, "Human Torch" },  // Powers/Player/HumanTorch/SummonFireHotspotVulnerability.prototype
        {   5299u, "Magik" },  // Powers/Player/Magik/Talents/Talent1SoulConeProjectiles.prototype
        {   5302u, "Storm" },  // Powers/Player/Storm/Traits/DefenseTrait.prototype
        {   5304u, "Black Widow" },  // Powers/Player/BlackWidow/RapidShotMissileEffect.prototype
        {   5305u, "Gambit" },  // Powers/Player/Gambit/FullEnduranceGain.prototype
        {   5306u, "War Machine" },  // Powers/Player/WarMachine/LifeSupportSelfRez.prototype
        {   5308u, "Elektra" },  // Powers/Player/Elektra/MarkForDeathOnDeathChargeGain.prototype
        {   5312u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/UltimateFuriousLunge.prototype
        {   5314u, "Iron Man" },  // Powers/Player/IronMan/WristRocketMissileEffect.prototype
        {   5317u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BamfBeatdownStrafe.prototype
        {   5318u, "Beast" },  // Powers/Player/Beast/HulkingSlamJubilee.prototype
        {   5319u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/RavenousBinding.prototype
        {   5321u, "Deadpool" },  // Powers/Player/Deadpool/Talents/BleedEmDryTalent.prototype
        {   5322u, "Magneto" },  // Powers/Player/Magneto/RapidFireMissileEffect.prototype
        {   5323u, "Black Cat" },  // Powers/Player/BlackCat/Assassinate.prototype
        {   5327u, "Iron Man" },  // Powers/Player/IronMan/ShieldOverloadCCImmuneCombo.prototype
        {   5329u, "Black Widow" },  // Powers/Player/BlackWidow/PistolShotMEffect.prototype
        {   5332u, "Hulk" },  // Powers/Player/Hulk/Rework/SmashFaceBleedComboVeryAngry.prototype
        {   5333u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/Focus.prototype
        {   5336u, "Iron Fist" },  // Powers/Player/IronFist/ChiBurstStanceVersion.prototype
        {   5337u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/GrootDeathFromAbove.prototype
        {   5338u, "Carnage" },  // Powers/Player/Carnage/UltimateHiddenPassive.prototype
        {   5340u, "Colossus" },  // Powers/Player/Colossus/ArmoringPunchComboEffect.prototype
        {   5341u, "Hulk" },  // Powers/Player/Hulk/Rework/RawrWeakenCombo.prototype
        {   5342u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelAttackResetLifespanCombo.prototype
        {   5343u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TelepathicIllusionJeanSummonHotspotEffect.prototype
        {   5348u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Talents/Talent1NonChaosBuff.prototype
        {   5349u, "Loki" },  // Powers/Player/Loki/LightBeamSummonComboMore.prototype
        {   5350u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/AcornMeteor.prototype
        {   5353u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/RobbieReyesDriveByMissileEffect.prototype
        {   5355u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Talents/Talent4MultVsLockheedDoTs.prototype
        {   5357u, "Hawkeye" },  // Powers/Player/Hawkeye/VolleyVisualCombo.prototype
        {   5358u, "Black Panther" },  // Powers/Player/BlackPanther/Ultimate.prototype
        {   5361u, "Storm" },  // Powers/Player/Storm/BallLightningEffect.prototype
        {   5362u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/WarpTurretSummonAreaTalented.prototype
        {   5363u, "Storm" },  // Powers/Player/Storm/UltimateIceEffect.prototype
        {   5374u, "Luke Cage" },  // Powers/Player/LukeCage/Talents/FightingStyleTheDefender.prototype
        {   5375u, "Dr. Doom" },  // Powers/Player/DrDoom/BallLightningCharges.prototype
        {   5377u, "Ant-Man" },  // Powers/Player/AntMan/RapidShrinkStrikeAntCombo.prototype
        {   5380u, "Hawkeye" },  // Powers/Player/Hawkeye/UltimateHiddenPassive.prototype
        {   5383u, "Thing" },  // Powers/Player/Thing/JaggedDefenderProcEffect.prototype
        {   5384u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/IllusionBonus.prototype
        {   5387u, "Captain America" },  // Powers/Player/CaptainAmerica/OnBlockGainPips.prototype
        {   5388u, "War Machine" },  // Powers/Player/WarMachine/ChainsawImpale.prototype
        {   5390u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/ConsumeHeatBuilder.prototype
        {   5394u, "Ghost Rider" },  // Powers/Player/GhostRider/FirePillarHealDefenseHotspotEffect.prototype
        {   5396u, "Colossus" },  // Powers/Player/Colossus/MovementSpinComboSummonUpgraded.prototype
        {   5397u, "Angela" },  // Powers/Player/Angela/HevensWrathGainMechanic.prototype
        {   5401u, "Nightcrawler" },  // Powers/Player/Nightcrawler/TeleportUseProc.prototype
        {   5402u, "Cable" },  // Powers/Player/Cable/TKSpearSlamBuffCombo.prototype
        {   5403u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/DisengagingShotMissileEffect.prototype
        {   5407u, "Iron Man" },  // Powers/Player/IronMan/UnibeamUpgraded.prototype
        {   5408u, "Psylocke" },  // Powers/Player/Psylocke/Traits/DefenseTrait.prototype
        {   5413u, "Iron Fist" },  // Powers/Player/IronFist/IronFistPunchImmobilizeTarget.prototype
        {   5417u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Traits/DefaultAmmoRegenEnd.prototype
        {   5419u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCardMeleeStage2Damage.prototype
        {   5422u, "Doctor Strange" },  // Powers/Player/DoctorStrange/DemonsOfDenakTransfer4.prototype
        {   5424u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/KittyPrydeDeathFromBelowEnd.prototype
        {   5427u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ThorGodlyValorSpiritGainPerHit.prototype
        {   5430u, "Thor" },  // Powers/Player/Thor/Rework/PBAoEStormSlowEffect.prototype
        {   5431u, "Hawkeye" },  // Powers/Player/Hawkeye/TumbleHasteCombo.prototype
        {   5433u, "X-23" },  // Powers/Player/X23/GrievousWounds360AoE150.prototype
        {   5434u, "Luke Cage" },  // Powers/Player/LukeCage/Pummel1stAttack.prototype
        {   5437u, "Black Panther" },  // Powers/Player/BlackPanther/SnareHotspotDamage.prototype
        {   5439u, "Ant-Man" },  // Powers/Player/AntMan/MultiStrike.prototype
        {   5441u, "Carnage" },  // Powers/Player/Carnage/Traits/OffenseTrait.prototype
        {   5442u, "Deadpool" },  // Powers/Player/Deadpool/Rework/ServerLagBossCombo.prototype
        {   5445u, "Iron Fist" },  // Powers/Player/IronFist/ChiSteroidDragonStanceBuff.prototype
        {   5446u, "Ultron" },  // Powers/Player/Ultron/RadiationBlastEnergySpec.prototype
        {   5450u, "Psylocke" },  // Powers/Player/Psylocke/KatanaLeapSlashAoEDoTMental.prototype
        {   5452u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/JuggernautImInvulnerableRevive.prototype
        {   5453u, "Magik" },  // Powers/Player/Magik/BFLDLeapAttack.prototype
        {   5454u, "Black Bolt" },  // Powers/Player/BlackBolt/Dash.prototype
        {   5459u, "Hulk" },  // Powers/Player/Hulk/Rework/PassiveToughReviveInvulnCombo.prototype
        {   5460u, "Loki" },  // Powers/Player/Loki/DecoyTurretsSorcerousBlast.prototype
        {   5462u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/CrossbonesMeleeDamageProc.prototype
        {   5464u, "Vision" },  // Powers/Player/Vision/SolarfFistsProcEffectPetVersionNoCosts.prototype
        {   5466u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MalekithDarkBeamHiddenPassive.prototype
        {   5469u, "Black Widow" },  // Powers/Player/BlackWidow/RapidShotPistolShotBleed.prototype
        {   5472u, "Captain America" },  // Powers/Player/CaptainAmerica/SoundRicochetMissileEffectSerum.prototype
        {   5474u, "Dr. Doom" },  // Powers/Player/DrDoom/ServoGuardArmyDoombotFlyers.prototype
        {   5475u, "Colossus" },  // Powers/Player/Colossus/MovementSpin.prototype
        {   5478u, "Colossus" },  // Powers/Player/Colossus/Traits/DefenseTrait.prototype
        {   5480u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CosmicGiftPower.prototype
        {   5482u, "Psylocke" },  // Powers/Player/Psylocke/KatanaPBAoEMental.prototype
        {   5487u, "Iceman" },  // Powers/Player/Iceman/HotspotBeamMelee.prototype
        {   5489u, "Nick Fury" },  // Powers/Player/NickFury/Tumble.prototype
        {   5490u, "Juggernaut" },  // Powers/Player/Juggernaut/MomentumPunchMomentumGain.prototype
        {   5492u, "Human Torch" },  // Powers/Player/HumanTorch/UltimateExplosionDoT.prototype
        {   5494u, "Loki" },  // Powers/Player/Loki/MainSpecRangedBuffDarkness.prototype
        {   5495u, "Green Goblin" },  // Powers/Player/GreenGoblin/GoblinCannonBlastAreaEffect.prototype
        {   5498u, "Colossus" },  // Powers/Player/Colossus/KittyPrydeSummon/DefaultAttack.prototype
        {   5499u, "Dr. Doom" },  // Powers/Player/DrDoom/MagicLanceMissileEffect.prototype
        {   5500u, "Venom" },  // Powers/Player/Venom/SigFreakoutImplosionComboBig.prototype
        {   5501u, "Juggernaut" },  // Powers/Player/Juggernaut/RemoveHighlight.prototype
        {   5503u, "Captain America" },  // Powers/Player/CaptainAmerica/Traits/DefenseTrait.prototype
        {   5506u, "Vision" },  // Powers/Player/Vision/DeathfromBelow.prototype
        {   5508u, "Ultron" },  // Powers/Player/Ultron/GroundThrowMissileEffect.prototype
        {   5510u, "Magik" },  // Powers/Player/Magik/LifeTapFilterPower.prototype
        {   5511u, "Cyclops" },  // Powers/Player/Cyclops/CarryTheMomentumEnduranceProc.prototype
        {   5512u, "Magik" },  // Powers/Player/Magik/BounceStrikeBounce.prototype
        {   5513u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/RavenousBindingSlowEffect.prototype
        {   5515u, "Carnage" },  // Powers/Player/Carnage/OrganicWebbingRangedBuff.prototype
        {   5519u, "Magneto" },  // Powers/Player/Magneto/SpawnMetalOrbHiddenPassive.prototype
        {   5521u, "Rogue" },  // Powers/Player/Rogue/RecallOverloadHotspotEffect.prototype
        {   5522u, "Elektra" },  // Powers/Player/Elektra/Traits/OffenseTrait.prototype
        {   5524u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/UltimateHotspotNoTeleportEffect.prototype
        {   5525u, "Beast" },  // Powers/Player/Beast/Talents/Talent4BrainsSpiritBuffProc.prototype
        {   5526u, "Hulk" },  // Powers/Player/Hulk/TremorsHotspotEffect.prototype
        {   5527u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MsMarvelConeRapidPunch.prototype
        {   5530u, "Doctor Strange" },  // Powers/Player/DoctorStrange/AstralFormAutoRevive.prototype
        {   5531u, "Daredevil" },  // Powers/Player/Daredevil/UltimateSaiThrowMissileEffect.prototype
        {   5534u, "Taskmaster" },  // Powers/Player/Taskmaster/UltimateBlackWidowTwilightInitiativeDoTStunCombo.prototype
        {   5537u, "Iron Fist" },  // Powers/Player/IronFist/DamageAbsorptionShield.prototype
        {   5540u, "Black Widow" },  // Powers/Player/BlackWidow/WidowsBiteChainEffect.prototype
        {   5546u, "She-Hulk" },  // Powers/Player/SheHulk/UltimateFinalHit.prototype
        {   5547u, "Rogue" },  // Powers/Player/Rogue/ChargeProcEffect.prototype
        {   5548u, "Emma Frost" },  // Powers/Player/EmmaFrost/ControlMobComboControlTarget.prototype
        {   5550u, "Deadpool" },  // Powers/Player/Deadpool/StopDropCCImmuneCombo.prototype
        {   5551u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/WhirlwindHotspotEffectBouncy.prototype
        {   5553u, "War Machine" },  // Powers/Player/WarMachine/HeatDecay.prototype
        {   5554u, "Iron Fist" },  // Powers/Player/IronFist/TigerStanceVisual.prototype
        {   5555u, "Deadpool" },  // Powers/Player/Deadpool/Rework/StabbyFlipBleed.prototype
        {   5556u, "Iceman" },  // Powers/Player/Iceman/Talents/ShatterBonus.prototype
        {   5557u, "Hulk" },  // Powers/Player/Hulk/Traits/AngerFull.prototype
        {   5561u, "Captain America" },  // Powers/Player/CaptainAmerica/Talents/PBAoEShieldThrow.prototype
        {   5562u, "Beast" },  // Powers/Player/Beast/BeastBamfBrosHotspotEffect.prototype
        {   5564u, "Ghost Rider" },  // Powers/Player/GhostRider/ChainFlechette.prototype
        {   5568u, "Elektra" },  // Powers/Player/Elektra/Talents/KnifeThrowAssassinateStealthTalent.prototype
        {   5569u, "Punisher" },  // Powers/Player/Punisher/ClaymoreExplosion.prototype
        {   5571u, "She-Hulk" },  // Powers/Player/SheHulk/Traits/MechanicTraitComboPoints.prototype
        {   5575u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/KaeciliusHealChannelErruptionsTargeted.prototype
        {   5577u, "Loki" },  // Powers/Player/Loki/DarkBoltChainCombo.prototype
        {   5579u, "Deadpool" },  // Powers/Player/Deadpool/Traits/OffenseTrait.prototype
        {   5581u, "Ghost Rider" },  // Powers/Player/GhostRider/BasicChains.prototype
        {   5582u, "Vision" },  // Powers/Player/Vision/ModeToggleSwitchToPhasePassThroughObjectVisual.prototype
        {   5586u, "Beast" },  // Powers/Player/Beast/SleepGasGadgetDamageHotspotEffect.prototype
        {   5587u, "Luke Cage" },  // Powers/Player/LukeCage/SweetChristmasPetCooldownReset.prototype
        {   5588u, "She-Hulk" },  // Powers/Player/SheHulk/HostileWitness.prototype
        {   5591u, "Ghost Rider" },  // Powers/Player/GhostRider/FireBreathBasicFireballDoTStack.prototype
        {   5592u, "Magneto" },  // Powers/Player/Magneto/DebrisShotMissileEffect.prototype
        {   5593u, "Gambit" },  // Powers/Player/Gambit/PassiveSleightOfHandDeathEffect.prototype
        {   5594u, "War Machine" },  // Powers/Player/WarMachine/MissilePods.prototype
        {   5595u, "Cyclops" },  // Powers/Player/Cyclops/FocusBeamStackCounter.prototype
        {   5596u, "Cable" },  // Powers/Player/Cable/PsimitarLungeDoT.prototype
        {   5597u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/SpideysDexterityTalent.prototype
        {   5600u, "Storm" },  // Powers/Player/Storm/TornadoHitEffect.prototype
        {   5601u, "Ultron" },  // Powers/Player/Ultron/Talents/Talent3CrushBuff.prototype
        {   5608u, "War Machine" },  // Powers/Player/WarMachine/AntiTankRoundsCondition.prototype
        {   5609u, "Deadpool" },  // Powers/Player/Deadpool/Rework/OffensePassiveHiddenPassive.prototype
        {   5611u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotExplosionDamageReductionCombo.prototype
        {   5612u, "Beast" },  // Powers/Player/Beast/ElectroGadgetKillSleepGadgets.prototype
        {   5615u, "Thor" },  // Powers/Player/Thor/Rework/BigDFAWeakenCombo.prototype
        {   5616u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/AcornMeteor2ndHit.prototype
        {   5619u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent5ProjectionsWeapons.prototype
        {   5624u, "She-Hulk" },  // Powers/Player/SheHulk/OpeningStatementEnd.prototype
        {   5627u, "Deadpool" },  // Powers/Player/Deadpool/Rework/GiantMallet.prototype
        {   5628u, "Dr. Doom" },  // Powers/Player/DrDoom/TeleportMagicRegenCombo.prototype
        {   5630u, "Moon Knight" },  // Powers/Player/MoonKnight/CrescentBola.prototype
        {   5631u, "Venom" },  // Powers/Player/Venom/SymbioteDrainPowerWhite1.prototype
        {   5632u, "Nova" },  // Powers/Player/Nova/LungingPunchCombo.prototype
        {   5634u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TelepathicIllusionPhoenixSummonHotspotEffect.prototype
        {   5637u, "Iceman" },  // Powers/Player/Iceman/ShatterFilterPower.prototype
        {   5638u, "Daredevil" },  // Powers/Player/Daredevil/OpeningLungeComboPointGain.prototype
        {   5641u, "Cyclops" },  // Powers/Player/Cyclops/Talents/SigCooldownTimeCDRProc.prototype
        {   5642u, "Elektra" },  // Powers/Player/Elektra/KnifeRopeChainMaxChannelTime.prototype
        {   5644u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotFlyerSkillshot3.prototype
        {   5646u, "Ant-Man" },  // Powers/Player/AntMan/PymSuit.prototype
        {   5648u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceGainCombo30.prototype
        {   5652u, "Angela" },  // Powers/Player/Angela/SigNoMatchMovementCombo.prototype
        {   5657u, "Daredevil" },  // Powers/Player/Daredevil/HighlightRemove.prototype
        {   5658u, "Iron Fist" },  // Powers/Player/IronFist/PummelDamageShieldCombo.prototype
        {   5659u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BrevikCowbell.prototype
        {   5660u, "Nova" },  // Powers/Player/Nova/PulsarSpiritRestoration.prototype
        {   5662u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveGamoraProcEffect.prototype
        {   5663u, "Thor" },  // Powers/Player/Thor/Rework/RagnarokForward.prototype
        {   5665u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent5AoEDoTBuff.prototype
        {   5668u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/BlackHoleInstagib.prototype
        {   5669u, "Nova" },  // Powers/Player/Nova/BasicPunchShieldRestore.prototype
        {   5671u, "Black Bolt" },  // Powers/Player/BlackBolt/KillingWord.prototype
        {   5673u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LokiIllusionVisualPassive.prototype
        {   5674u, "Luke Cage" },  // Powers/Player/LukeCage/ChunkOConcreteMissileEffect.prototype
        {   5675u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Talents/Talent4LockheedSuperCharge.prototype
        {   5680u, "Deadpool" },  // Powers/Player/Deadpool/Traits/MechanicTraitAwesome.prototype
        {   5682u, "Captain America" },  // Powers/Player/CaptainAmerica/ShieldBash.prototype
        {   5683u, "Iron Fist" },  // Powers/Player/IronFist/Pummel2ndAttack.prototype
        {   5685u, "Angela" },  // Powers/Player/Angela/SwordLungeEffect.prototype
        {   5686u, "Nova" },  // Powers/Player/Nova/ChannelPulsarBeamEnhanced.prototype
        {   5690u, "Human Torch" },  // Powers/Player/HumanTorch/Traits/OverheatEffect.prototype
        {   5693u, "Storm" },  // Powers/Player/Storm/WindSpecDustDevilHotspotPassive.prototype
        {   5698u, "Angela" },  // Powers/Player/Angela/WhippingRibbonsYank.prototype
        {   5704u, "Storm" },  // Powers/Player/Storm/TyphoonAcidRainHotspotEffect.prototype
        {   5707u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DamageConeWeaken.prototype
        {   5708u, "Elektra" },  // Powers/Player/Elektra/CrossStrikeEnd.prototype
        {   5712u, "Taskmaster" },  // Powers/Player/Taskmaster/CoupDeGrace.prototype
        {   5715u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/JessicaJones/JessicaJonesDeathFromAbove.prototype
        {   5717u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveRedSkullProcEffectDoT.prototype
        {   5722u, "Beast" },  // Powers/Player/Beast/SigMomentumRestoreOverTime.prototype
        {   5728u, "Ant-Man" },  // Powers/Player/AntMan/AnthillActive.prototype
        {   5729u, "Doctor Strange" },  // Powers/Player/DoctorStrange/DemonsOfDenakHiddenPassiveProcEf.prototype
        {   5732u, "Blade" },  // Powers/Player/Blade/AdvancedTechniqueCombo2Used.prototype
        {   5736u, "Magneto" },  // Powers/Player/Magneto/Talents/HomingBlastBonus.prototype
        {   5739u, "Thor" },  // Powers/Player/Thor/Rework/RagnarokForwardKeywordConditionCombo.prototype
        {   5740u, "Loki" },  // Powers/Player/Loki/SoulCrushTransfer4.prototype
        {   5744u, "Storm" },  // Powers/Player/Storm/StormSurgeFreezingTempestSummonAgent.prototype
        {   5749u, "Doctor Strange" },  // Powers/Player/DoctorStrange/SummonFlames.prototype
        {   5754u, "Storm" },  // Powers/Player/Storm/DisengagingShotCombo.prototype
        {   5756u, "Black Cat" },  // Powers/Player/BlackCat/GasTrap.prototype
        {   5757u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/TacticsGunsHiddenPassive.prototype
        {   5758u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/SpikedBallSkillshotMissileEffect.prototype
        {   5759u, "Thor" },  // Powers/Player/Thor/Talents/BasicMeleeLightningBoltTalent.prototype
        {   5760u, "Cyclops" },  // Powers/Player/Cyclops/Rework/PassiveLeaderCleanseCombo.prototype
        {   5762u, "Hulk" },  // Powers/Player/Hulk/Rework/LeapImplode.prototype
        {   5763u, "Green Goblin" },  // Powers/Player/GreenGoblin/Dash.prototype
        {   5765u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/FirestarEnergyRainStart.prototype
        {   5766u, "Iron Fist" },  // Powers/Player/IronFist/LeopardStanceVisual.prototype
        {   5767u, "Thor" },  // Powers/Player/Thor/Rework/RagnarokForwardInnerCombo.prototype
        {   5770u, "Venom" },  // Powers/Player/Venom/SymbioteDrainPower3.prototype
        {   5772u, "Beast" },  // Powers/Player/Beast/MomentumDecayasCombo.prototype
        {   5774u, "Iceman" },  // Powers/Player/Iceman/RapidFireMissileEffect.prototype
        {   5775u, "Black Cat" },  // Powers/Player/BlackCat/MasterThiefHiddenPassive.prototype
        {   5777u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/RitualCircleBuffEffect.prototype
        {   5778u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MrSinisterAstralProjectionBuff.prototype
        {   5779u, "Wolverine" },  // Powers/Player/Wolverine/BloodySteroidAsCombo.prototype
        {   5781u, "Magneto" },  // Powers/Player/Magneto/Traits/OffenseTrait.prototype
        {   5782u, "Storm" },  // Powers/Player/Storm/MicroburstBigger.prototype
        {   5783u, "X-23" },  // Powers/Player/X23/GrievousWoundsWedge90.prototype
        {   5784u, "Blade" },  // Powers/Player/Blade/JustStayDownUVGrenadeCombo.prototype
        {   5785u, "Ant-Man" },  // Powers/Player/AntMan/AntnadoMovementPower.prototype
        {   5790u, "Hawkeye" },  // Powers/Player/Hawkeye/TumbleStunEffect.prototype
        {   5795u, "Doctor Strange" },  // Powers/Player/DoctorStrange/BasicDaggersHiddenPassive.prototype
        {   5805u, "Hulk" },  // Powers/Player/Hulk/Rework/CarFists.prototype
        {   5808u, "Ultron" },  // Powers/Player/Ultron/RadiationBlastPulseController.prototype
        {   5809u, "Beast" },  // Powers/Player/Beast/Talents/Talent3PummelCDR.prototype
        {   5812u, "Colossus" },  // Powers/Player/Colossus/SignatureCDR.prototype
        {   5813u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/StealthToggleOnFocusSpendProcEffect.prototype
        {   5814u, "Venom" },  // Powers/Player/Venom/Talents/SymbioteDrainBuff.prototype
        {   5815u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/NeuralNetworkTransfer2ndWave.prototype
        {   5826u, "Gambit" },  // Powers/Player/Gambit/Traits/OffenseTrait.prototype
        {   5828u, "Hulk" },  // Powers/Player/Hulk/HulkMadProcEffect.prototype
        {   5832u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/SignatureNukeSpiritGainCombo.prototype
        {   5834u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Talents/Talent3Burrow.prototype
        {   5836u, "Doctor Strange" },  // Powers/Player/DoctorStrange/ProjectionWindsOfWatoomb.prototype
        {   5837u, "Carnage" },  // Powers/Player/Carnage/Talents/HyperMobile.prototype
        {   5838u, "Captain America" },  // Powers/Player/CaptainAmerica/ShieldBlock.prototype
        {   5841u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/DisengagingShotMissileCombo.prototype
        {   5842u, "Thing" },  // Powers/Player/Thing/Traits/DefenseTrait.prototype
        {   5844u, "She-Hulk" },  // Powers/Player/SheHulk/ComboPointHiddenPassive.prototype
        {   5845u, "X-23" },  // Powers/Player/X23/UltimateHiddenPassive.prototype
        {   5850u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Talents/Talent3ShieldBoostHealthRegen.prototype
        {   5851u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Talents/Talent1LockheedPassive.prototype
        {   5857u, "Captain America" },  // Powers/Player/CaptainAmerica/FirstStrikeEnduranceGain.prototype
        {   5859u, "Punisher" },  // Powers/Player/Punisher/Talents/HollowpointRoundsDoT.prototype
        {   5860u, "Taskmaster" },  // Powers/Player/Taskmaster/RoundhouseEnabled.prototype
        {   5864u, "Cable" },  // Powers/Player/Cable/ViperBeamPlus.prototype
        {   5865u, "Rogue" },  // Powers/Player/Rogue/RecallOverloadHotspotEffectMental.prototype
        {   5867u, "Punisher" },  // Powers/Player/Punisher/Rework/HighlightReloadConditionRemoval.prototype
        {   5873u, "Black Bolt" },  // Powers/Player/BlackBolt/Talents/Talent3BarrierRemap.prototype
        {   5878u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Talents/Talent1ChaosBuff.prototype
        {   5880u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent2SigButterflyCDR.prototype
        {   5883u, "Magik" },  // Powers/Player/Magik/LimboDemonLeapAttackEnd.prototype
        {   5885u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosBlastMissileEffect.prototype
        {   5886u, "Black Widow" },  // Powers/Player/BlackWidow/WidowmakerGain.prototype
        {   5891u, "Ultron" },  // Powers/Player/Ultron/UltimateBigHit.prototype
        {   5892u, "War Machine" },  // Powers/Player/WarMachine/ChaingunFullAutoMissileEffect.prototype
        {   5896u, "Human Torch" },  // Powers/Player/HumanTorch/SummonFireWallSoundOnly.prototype
        {   5899u, "Beast" },  // Powers/Player/Beast/FireBombHotspotEffect.prototype
        {   5902u, "Green Goblin" },  // Powers/Player/GreenGoblin/SummonMoreSonicToads.prototype
        {   5904u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/DarkHexAsCombo.prototype
        {   5905u, "Hawkeye" },  // Powers/Player/Hawkeye/TaserArrow.prototype
        {   5906u, "Moon Knight" },  // Powers/Player/MoonKnight/UltimateDamageSummon.prototype
        {   5908u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/UltimateFantasticFour.prototype
        {   5909u, "Nick Fury" },  // Powers/Player/NickFury/Traits/DefaultAmmoRegenTrigger.prototype
        {   5911u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/SignatureTKHurlJeanSummon.prototype
        {   5912u, "Cyclops" },  // Powers/Player/Cyclops/Talents/BeamBleedTalent.prototype
        {   5913u, "Ant-Man" },  // Powers/Player/AntMan/AntPetDefaultAttack.prototype
        {   5917u, "Venom" },  // Powers/Player/Venom/SigFreakoutComboImpale.prototype
        {   5918u, "Magneto" },  // Powers/Player/Magneto/Talents/AutoDebrisFling.prototype
        {   5920u, "Emma Frost" },  // Powers/Player/EmmaFrost/KneelBeforeMeHotspotKnockdown.prototype
        {   5922u, "Thor" },  // Powers/Player/Thor/Rework/ThunderSpotAreaDamageEffect.prototype
        {   5923u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/AppliedBarrier.prototype
        {   5926u, "Carnage" },  // Powers/Player/Carnage/BasicClawsHealthOnHitCombo.prototype
        {   5928u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CosmicWakeStopWaking.prototype
        {   5931u, "Beast" },  // Powers/Player/Beast/PsychicField.prototype
        {   5932u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/JessicaJones/ThrowConcreteMissileEffect.prototype
        {   5933u, "Hawkeye" },  // Powers/Player/Hawkeye/PinningShot.prototype
        {   5934u, "Luke Cage" },  // Powers/Player/LukeCage/BasicChainWhip.prototype
        {   5937u, "She-Hulk" },  // Powers/Player/SheHulk/BarristerBeatdown6thHit.prototype
        {   5940u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelBombsDeployAcorn.prototype
        {   5941u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveCosmicDoop.prototype
        {   5942u, "Deadpool" },  // Powers/Player/Deadpool/Rework/PowerUpsProcEffect.prototype
        {   5944u, "Hulk" },  // Powers/Player/Hulk/Rework/LeapQuakeEndRouter.prototype
        {   5948u, "Thor" },  // Powers/Player/Thor/Ultimate.prototype
        {   5949u, "Black Widow" },  // Powers/Player/BlackWidow/RollingGrenades6.prototype
        {   5950u, "Nova" },  // Powers/Player/Nova/Talents/Talent2PulsarSpiritRestoreDmgStack.prototype
        {   5951u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceVisualAndDamageBuff.prototype
        {   5952u, "Loki" },  // Powers/Player/Loki/UltimateHiddenPassive.prototype
        {   5955u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/DefenseHotspot.prototype
        {   5956u, "Black Cat" },  // Powers/Player/BlackCat/UltimateHiddenPassive.prototype
        {   5957u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MrSinisterAstralProjectionDelay.prototype
        {   5958u, "Taskmaster" },  // Powers/Player/Taskmaster/Tumble.prototype
        {   5964u, "Ultron" },  // Powers/Player/Ultron/SpinAttack.prototype
        {   5968u, "Hawkeye" },  // Powers/Player/Hawkeye/KatanaEffectBleed.prototype
        {   5970u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent1RemoveComboPointsBuff.prototype
        {   5971u, "Taskmaster" },  // Powers/Player/Taskmaster/WidowsBiteVisualBeam.prototype
        {   5972u, "Hulk" },  // Powers/Player/Hulk/Rework/DashCrash.prototype
        {   5973u, "Magneto" },  // Powers/Player/Magneto/Talents/BoomerangScrap.prototype
        {   5974u, "Beast" },  // Powers/Player/Beast/HulkingSlamJubileeHotspotEffect.prototype
        {   5975u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallIcemanHotspotSlowEffect.prototype
        {   5977u, "Black Panther" },  // Powers/Player/BlackPanther/Traits/OffenseTrait.prototype
        {   5978u, "Loki" },  // Powers/Player/Loki/IllusionRush.prototype
        {   5979u, "X-23" },  // Powers/Player/X23/HealCleanseSelfRezInvulnerabilityCombo.prototype
        {   5981u, "Taskmaster" },  // Powers/Player/Taskmaster/SwingingAssaultHitEffect.prototype
        {   5982u, "Punisher" },  // Powers/Player/Punisher/Rework/ChemicalBombHotspotEffect.prototype
        {   5984u, "Deadpool" },  // Powers/Player/Deadpool/Rework/BangBangMissileEffectExplosive.prototype
        {   5989u, "Luke Cage" },  // Powers/Player/LukeCage/GoodAtCombosAttackSpeedBuff.prototype
        {   5995u, "Psylocke" },  // Powers/Player/Psylocke/ImplosionEffect.prototype
        {   6000u, "Angela" },  // Powers/Player/Angela/DisablingRibbonsVulnCombo.prototype
        {   6001u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/ForcePillarBonus.prototype
        {   6003u, "Iceman" },  // Powers/Player/Iceman/AbsoluteZeroHotspot.prototype
        {   6008u, "Colossus" },  // Powers/Player/Colossus/GroundStompCooldown.prototype
        {   6009u, "Luke Cage" },  // Powers/Player/LukeCage/Talents/FightingStyleFistsOfFury.prototype
        {   6010u, "Thing" },  // Powers/Player/Thing/Rework/WeaponsBuffProc.prototype
        {   6012u, "Gambit" },  // Powers/Player/Gambit/UltimateCardThrow2.prototype
        {   6014u, "Ant-Man" },  // Powers/Player/AntMan/Talents/STSSAntRecharge100Pct.prototype
        {   6017u, "Rogue" },  // Powers/Player/Rogue/UltimateDashSlash2.prototype
        {   6020u, "War Machine" },  // Powers/Player/WarMachine/BulletOneOff.prototype
        {   6021u, "Green Goblin" },  // Powers/Player/GreenGoblin/ExplosivePumpkinInnerCombo.prototype
        {   6023u, "Carnage" },  // Powers/Player/Carnage/Talents/SaferPlay.prototype
        {   6025u, "Hulk" },  // Powers/Player/Hulk/Rework/BasicMeleeUtilHealthGain.prototype
        {   6027u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondPowerComboResourceGain.prototype
        {   6031u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/IronFistTechniqueLeopardSlashAoEProcEffect.prototype
        {   6032u, "Taskmaster" },  // Powers/Player/Taskmaster/SwordStrikeTwo.prototype
        {   6035u, "Ant-Man" },  // Powers/Player/AntMan/Traits/MechanicTraitAnts.prototype
        {   6036u, "Beast" },  // Powers/Player/Beast/UltimateWreckingBallRandomLocation.prototype
        {   6041u, "Deadpool" },  // Powers/Player/Deadpool/Rework/SaiAssaultAttackSpeedCombo.prototype
        {   6045u, "Gambit" },  // Powers/Player/Gambit/UltimateAreaExplosion.prototype
        {   6049u, "Colossus" },  // Powers/Player/Colossus/NightcrawlerSummon/DefaultAttack3.prototype
        {   6053u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LokiIllusionMeleeAttack.prototype
        {   6055u, "X-23" },  // Powers/Player/X23/Talents/Talent5DefenseBuffs.prototype
        {   6057u, "Deadpool" },  // Powers/Player/Deadpool/Rework/BiggerLilDeadpoolOnDeathTrigger.prototype
        {   6059u, "Black Panther" },  // Powers/Player/BlackPanther/SmokeScreenSpecRangedBuff.prototype
        {   6063u, "Vision" },  // Powers/Player/Vision/DensePunchDefenseDamageReductionBonus.prototype
        {   6069u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseAoESummonCombo.prototype
        {   6072u, "Thor" },  // Powers/Player/Thor/Rework/HammerThrow.prototype
        {   6077u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/AstralLegion.prototype
        {   6078u, "Wolverine" },  // Powers/Player/Wolverine/FlyingBleed.prototype
        {   6079u, "Daredevil" },  // Powers/Player/Daredevil/Talents/WhirlingClubStaminaCancelTalent.prototype
        {   6082u, "Cable" },  // Powers/Player/Cable/KineticRepulsionSmallKnockback.prototype
        {   6088u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateCallAngel.prototype
        {   6090u, "Luke Cage" },  // Powers/Player/LukeCage/Ultimate.prototype
        {   6092u, "Green Goblin" },  // Powers/Player/GreenGoblin/GhostHealOnHit.prototype
        {   6093u, "Jean Grey" },  // Powers/Player/JeanGrey/UltimateHotspotSummonCombo.prototype
        {   6095u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Talents/Talent3RipApartReality.prototype
        {   6096u, "Moon Knight" },  // Powers/Player/MoonKnight/Ricochet.prototype
        {   6099u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/StealthToggleHiddenPassive.prototype
        {   6100u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AreaDoT.prototype
        {   6101u, "Magneto" },  // Powers/Player/Magneto/ShrapnelAuraMissilePower.prototype
        {   6103u, "Punisher" },  // Powers/Player/Punisher/Rework/PassiveToughHiddenPassive.prototype
        {   6106u, "Winter Soldier" },  // Powers/Player/WinterSoldier/StealthMineToss.prototype
        {   6111u, "Black Panther" },  // Powers/Player/BlackPanther/BasicDaggerThrow.prototype
        {   6114u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Talents/Talent1BionicBrawling.prototype
        {   6118u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveCarnageAttributeBuffRemoval.prototype
        {   6119u, "X-23" },  // Powers/Player/X23/Traits/OffenseTrait.prototype
        {   6120u, "Moon Knight" },  // Powers/Player/MoonKnight/PassiveMultiPersonality.prototype
        {   6121u, "Human Torch" },  // Powers/Player/HumanTorch/BouncingFireballsNewMissileEffec.prototype
        {   6122u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/NeuralNetworkJean.prototype
        {   6124u, "Hulk" },  // Powers/Player/Hulk/Rework/MeteorDebrisArtCombo.prototype
        {   6125u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Traits/DefaultAmmoRegen.prototype
        {   6127u, "Nightcrawler" },  // Powers/Player/Nightcrawler/DoubleSlash.prototype
        {   6136u, "Cable" },  // Powers/Player/Cable/Talents/ViperBeamLayer.prototype
        {   6142u, "Kitty Pryde" },  // Powers/Player/KittyPryde/UltimateInvulnerabilityCombo.prototype
        {   6149u, "Iron Fist" },  // Powers/Player/IronFist/ChiSteroidLeopardStanceBuff.prototype
        {   6150u, "Ant-Man" },  // Powers/Player/AntMan/Talents/TankerThrowTalent.prototype
        {   6151u, "Iceman" },  // Powers/Player/Iceman/ChanneledBeamDeepFreezer.prototype
        {   6152u, "Iceman" },  // Powers/Player/Iceman/HotspotBeamHotspotEffect.prototype
        {   6153u, "War Machine" },  // Powers/Player/WarMachine/ChaingunFullAutoMissileEffectAntiTank.prototype
        {   6154u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/JeanGreyPullTowardsPointDamage.prototype
        {   6157u, "Black Bolt" },  // Powers/Player/BlackBolt/EchoRangedBuffCombo.prototype
        {   6159u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Talents/Talent1FlashandGrabCooldown.prototype
        {   6162u, "Doctor Strange" },  // Powers/Player/DoctorStrange/ProjectionFangNuke.prototype
        {   6163u, "Black Widow" },  // Powers/Player/BlackWidow/TwilightInitiativeSlowAoECombo.prototype
        {   6164u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedToggleTargetingHotspotEffect.prototype
        {   6165u, "Beast" },  // Powers/Player/Beast/MeleePBAoEDamageBuff.prototype
        {   6169u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Talents/Talent2LockheedFocus.prototype
        {   6170u, "Angela" },  // Powers/Player/Angela/IchorBasic.prototype
        {   6178u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaoticDebuffResoundingWaves.prototype
        {   6182u, "Cable" },  // Powers/Player/Cable/Talents/TechnoOrganicSoldier.prototype
        {   6183u, "Nova" },  // Powers/Player/Nova/SignatureCDR.prototype
        {   6185u, "Psylocke" },  // Powers/Player/Psylocke/DashStealthLungeMovementBuffCombo.prototype
        {   6189u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHolePowerCosmicRegenEffect.prototype
        {   6191u, "Ultron" },  // Powers/Player/Ultron/Flamethrower.prototype
        {   6193u, "Juggernaut" },  // Powers/Player/Juggernaut/MomentumDecayAsComboRemoval.prototype
        {   6198u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/JetDashEffect.prototype
        {   6199u, "Vision" },  // Powers/Player/Vision/DensityHiddenPassiveController.prototype
        {   6203u, "Magneto" },  // Powers/Player/Magneto/UltimateSentinelSmashThrowables.prototype
        {   6206u, "Nick Fury" },  // Powers/Player/NickFury/Microdrones.prototype
        {   6207u, "Vision" },  // Powers/Player/Vision/Phase.prototype
        {   6208u, "Doctor Strange" },  // Powers/Player/DoctorStrange/DemonsOfDenak.prototype
        {   6211u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/PlasmaCannon.prototype
        {   6214u, "Human Torch" },  // Powers/Player/HumanTorch/UltimateHotspotEffect.prototype
        {   6215u, "Hulk" },  // Powers/Player/Hulk/Traits/MechanicTraitAnger.prototype
        {   6217u, "Psylocke" },  // Powers/Player/Psylocke/BowDecoyMissileEffect.prototype
        {   6218u, "Doctor Strange" },  // Powers/Player/DoctorStrange/ProjectionConeShards.prototype
        {   6219u, "Gambit" },  // Powers/Player/Gambit/GrandSlam2ndHit.prototype
        {   6221u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseDashEnergyRegenBuff.prototype
        {   6222u, "Black Bolt" },  // Powers/Player/BlackBolt/MasterBlowStart.prototype
        {   6224u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/DoomBallLightningMissileEffect.prototype
        {   6225u, "Magik" },  // Powers/Player/Magik/SoulswordBasic.prototype
        {   6226u, "Luke Cage" },  // Powers/Player/LukeCage/SummonJessicaJones.prototype
        {   6227u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/StretchyDashKnockdownEffect.prototype
        {   6230u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LokiIllusionMeleeAttack2.prototype
        {   6232u, "Ant-Man" },  // Powers/Player/AntMan/Talents/AntVulnerabilityTalent.prototype
        {   6234u, "Daredevil" },  // Powers/Player/Daredevil/OpeningLungeComboEffect.prototype
        {   6235u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/IcemanIceGolemHiddenPassiveToggler.prototype
        {   6237u, "Gambit" },  // Powers/Player/Gambit/BoWhirlwindComboSummon.prototype
        {   6240u, "Carnage" },  // Powers/Player/Carnage/Talents/MaceWeaponsChargesProcEffect.prototype
        {   6242u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MilesMoralesProcEffect.prototype
        {   6251u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/UltimateFFShield.prototype
        {   6254u, "Ghost Rider" },  // Powers/Player/GhostRider/Traits/DefenseTrait.prototype
        {   6257u, "Angela" },  // Powers/Player/Angela/SigNoMatchHealthGain.prototype
        {   6258u, "Cable" },  // Powers/Player/Cable/PsychicBulletsMissileEffect.prototype
        {   6261u, "Nova" },  // Powers/Player/Nova/ExplosionFromMovementRemovalCondition.prototype
        {   6262u, "Captain America" },  // Powers/Player/CaptainAmerica/ShieldBlockCooldownSerumSpecProcEffect.prototype
        {   6265u, "Rogue" },  // Powers/Player/Rogue/GlovesOffAsCombo.prototype
        {   6266u, "Thing" },  // Powers/Player/Thing/Talents/Talent3ClobberinTimeDefensiveBoost.prototype
        {   6267u, "Black Cat" },  // Powers/Player/BlackCat/DeathFromAboveBleed.prototype
        {   6272u, "Iron Man" },  // Powers/Player/IronMan/SystemRebootRevive.prototype
        {   6275u, "Rogue" },  // Powers/Player/Rogue/RecallOverloadPrepareEndExplosionMental.prototype
        {   6280u, "Venom" },  // Powers/Player/Venom/FearCleanseStunCombo.prototype
        {   6282u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/NeuralNetworkTransfer.prototype
        {   6284u, "Iron Man" },  // Powers/Player/IronMan/RapidFire.prototype
        {   6285u, "Ant-Man" },  // Powers/Player/AntMan/Antnado.prototype
        {   6288u, "Gambit" },  // Powers/Player/Gambit/CardPickup.prototype
        {   6289u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Traits/TriggerChaosOverload.prototype
        {   6290u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/SealOfHoggoth.prototype
        {   6293u, "Nick Fury" },  // Powers/Player/NickFury/ShieldAgentRifleAttack.prototype
        {   6295u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BasicBouncingBeam.prototype
        {   6297u, "Green Goblin" },  // Powers/Player/GreenGoblin/MadmanCleanseHiddenPassive.prototype
        {   6298u, "Angela" },  // Powers/Player/Angela/MiraculousAssault.prototype
        {   6306u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/BouncingBubbleDestructibleKiller.prototype
        {   6308u, "Hawkeye" },  // Powers/Player/Hawkeye/DoubleShotMissileEffect.prototype
        {   6309u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/SeekerOrbsMissileEffect.prototype
        {   6310u, "Angela" },  // Powers/Player/Angela/DoubleAxeThrowExecuteMissileEffect.prototype
        {   6313u, "She-Hulk" },  // Powers/Player/SheHulk/CrossExamination.prototype
        {   6314u, "Gambit" },  // Powers/Player/Gambit/Tumble.prototype
        {   6315u, "Punisher" },  // Powers/Player/Punisher/RocketRecharge.prototype
        {   6316u, "Punisher" },  // Powers/Player/Punisher/Talents/FlamethrowerBuff.prototype
        {   6320u, "Black Panther" },  // Powers/Player/BlackPanther/AcrobaticAttack.prototype
        {   6322u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosRift.prototype
        {   6324u, "Blade" },  // Powers/Player/Blade/Talents/StakeTalent.prototype
        {   6325u, "Magik" },  // Powers/Player/Magik/SoulCone.prototype
        {   6330u, "Rogue" },  // Powers/Player/Rogue/UltimateSeekerButterfliesSelfCondition.prototype
        {   6333u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GrootRide.prototype
        {   6335u, "Gambit" },  // Powers/Player/Gambit/UltimateComboTempInvulnerable.prototype
        {   6338u, "She-Hulk" },  // Powers/Player/SheHulk/ConvictionPBAoE.prototype
        {   6339u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/JessicaJonesThrowConcreteMissileEffect.prototype
        {   6341u, "Black Widow" },  // Powers/Player/BlackWidow/Plastique.prototype
        {   6342u, "Human Torch" },  // Powers/Player/HumanTorch/ConsumeBuffEffect.prototype
        {   6345u, "Punisher" },  // Powers/Player/Punisher/Rework/PassiveToughCleanseCower.prototype
        {   6346u, "Black Panther" },  // Powers/Player/BlackPanther/MineFieldMelee.prototype
        {   6350u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveCrossbones.prototype
        {   6351u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/ColleenWing/BladeDanceHotspotEffect.prototype
        {   6354u, "Nick Fury" },  // Powers/Player/NickFury/SummonMinigun.prototype
        {   6356u, "Luke Cage" },  // Powers/Player/LukeCage/Talents/ColleenWing.prototype
        {   6359u, "Angela" },  // Powers/Player/Angela/SwordPummelBleed.prototype
        {   6365u, "Iceman" },  // Powers/Player/Iceman/Talents/IceBlockCDDefensiveBuff.prototype
        {   6367u, "Iron Fist" },  // Powers/Player/IronFist/NinjutsuDashProcEffect.prototype
        {   6372u, "Ghost Rider" },  // Powers/Player/GhostRider/PassiveTeamDefenseShieldProc.prototype
        {   6373u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MagikMiniDemonDoubleStrike2ndHit.prototype
        {   6375u, "Juggernaut" },  // Powers/Player/Juggernaut/UnstoppableChargeHotspotKnockbac.prototype
        {   6376u, "Magneto" },  // Powers/Player/Magneto/ElectromagneticBeamCDR.prototype
        {   6378u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/HawkeyesPrecisionTalent.prototype
        {   6382u, "Iceman" },  // Powers/Player/Iceman/Talents/HailBallIcicleBall.prototype
        {   6383u, "Winter Soldier" },  // Powers/Player/WinterSoldier/StealthMineFearCombo.prototype
        {   6384u, "Captain America" },  // Powers/Player/CaptainAmerica/PatrioticTaunt.prototype
        {   6386u, "Iceman" },  // Powers/Player/Iceman/DeepFreezeEffectStrong.prototype
        {   6392u, "War Machine" },  // Powers/Player/WarMachine/Talents/Talent1LaserBlade.prototype
        {   6393u, "Rogue" },  // Powers/Player/Rogue/StolenPowerLibrarySlot3.prototype
        {   6395u, "Iceman" },  // Powers/Player/Iceman/FlightComboEffect.prototype
        {   6396u, "Loki" },  // Powers/Player/Loki/ChainBoltDestructibleKiller.prototype
        {   6409u, "Blade" },  // Powers/Player/Blade/LowRiskPowerCounter.prototype
        {   6411u, "Ghost Rider" },  // Powers/Player/GhostRider/FireBreathLarger.prototype
        {   6413u, "Blade" },  // Powers/Player/Blade/AdvancedTechniqueCooldownStart.prototype
        {   6416u, "Magneto" },  // Powers/Player/Magneto/UltimateSentinelSummonHS.prototype
        {   6417u, "Jean Grey" },  // Powers/Player/JeanGrey/DebrisMaelstromHotspotEffect.prototype
        {   6418u, "Rogue" },  // Powers/Player/Rogue/RapidPunchDash.prototype
        {   6422u, "Magik" },  // Powers/Player/Magik/DarkPactRemoveBuffs.prototype
        {   6425u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ArmyFromNothingIntervalEffect.prototype
        {   6427u, "War Machine" },  // Powers/Player/WarMachine/Traits/MechanicTrait.prototype
        {   6428u, "Nick Fury" },  // Powers/Player/NickFury/Talents/Talent5SignatureDangerCloseExtraShots.prototype
        {   6429u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/MinigunDamageBuff.prototype
        {   6431u, "Black Bolt" },  // Powers/Player/BlackBolt/AuraSummonCombo.prototype
        {   6437u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallBeastBleedEffect.prototype
        {   6438u, "Iron Man" },  // Powers/Player/IronMan/Talents/LifeSupport.prototype
        {   6439u, "Black Widow" },  // Powers/Player/BlackWidow/Traits/MechanicTraitElectricCharge.prototype
        {   6440u, "Hulk" },  // Powers/Player/Hulk/Rework/GammaPunch.prototype
        {   6441u, "Deadpool" },  // Powers/Player/Deadpool/Rework/Lunge.prototype
        {   6442u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent2DefenseBuffRemoveDisabler.prototype
        {   6444u, "Daredevil" },  // Powers/Player/Daredevil/HealingTranceEnduranceRegen.prototype
        {   6445u, "Luke Cage" },  // Powers/Player/LukeCage/ComboPointGainMechanic.prototype
        {   6446u, "Black Cat" },  // Powers/Player/BlackCat/Traits/DefenseTrait.prototype
        {   6448u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/HERBIESummonProc.prototype
        {   6449u, "Taskmaster" },  // Powers/Player/Taskmaster/FreezeArrowSummonHotspot.prototype
        {   6450u, "Ultron" },  // Powers/Player/Ultron/HomingMissiles.prototype
        {   6454u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ElectricAoEGadgetTeslaCoilLightningStrike.prototype
        {   6456u, "Moon Knight" },  // Powers/Player/MoonKnight/RapidFireRestoreCombo.prototype
        {   6458u, "Iceman" },  // Powers/Player/Iceman/AbsoluteZero.prototype
        {   6459u, "Hulk" },  // Powers/Player/Hulk/Rework/Tantrum2ndAttack.prototype
        {   6463u, "Iron Fist" },  // Powers/Player/IronFist/ChiBurstVisual.prototype
        {   6467u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Talents/Talent2HeavyGaussBuffs.prototype
        {   6472u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerDeadpoolTheKidDefaultAttack.prototype
        {   6476u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/DarkHex.prototype
        {   6477u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Unique315BoomerangBubble.prototype
        {   6478u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/ControlledMobToIllusion.prototype
        {   6479u, "Ultron" },  // Powers/Player/Ultron/PassiveSelfRezPreExplosionPlayer.prototype
        {   6481u, "Punisher" },  // Powers/Player/Punisher/Talents/InYourFaceBuffProc.prototype
        {   6482u, "Colossus" },  // Powers/Player/Colossus/ArmorDamageAbsorbStopperEnd.prototype
        {   6483u, "Ultron" },  // Powers/Player/Ultron/AlphaSpecDamageShield.prototype
        {   6490u, "Doctor Strange" },  // Powers/Player/DoctorStrange/ProjectionBasicDaggers.prototype
        {   6491u, "Punisher" },  // Powers/Player/Punisher/Rework/MinigunTargetAudioCombo.prototype
        {   6493u, "Doctor Strange" },  // Powers/Player/DoctorStrange/MysticEnergyVisualTrigger.prototype
        {   6499u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ImplosionAsCombo.prototype
        {   6500u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerCablePlasmaBarrage.prototype
        {   6502u, "Green Goblin" },  // Powers/Player/GreenGoblin/Talents/MoreGhostsTalent.prototype
        {   6504u, "Rogue" },  // Powers/Player/Rogue/UltimateTransformComboActivate.prototype
        {   6506u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHoleSummonPuller.prototype
        {   6508u, "War Machine" },  // Powers/Player/WarMachine/Traits/OffenseTrait.prototype
        {   6509u, "Iron Man" },  // Powers/Player/IronMan/UltimateMicroMissileEffect.prototype
        {   6511u, "Vision" },  // Powers/Player/Vision/EnhanceRobotBuffHotspotAoE.prototype
        {   6515u, "Vision" },  // Powers/Player/Vision/SigContinuedEnduranceGain.prototype
        {   6516u, "Blade" },  // Powers/Player/Blade/AdvancedTechniqueCombo2Spin3.prototype
        {   6518u, "Daredevil" },  // Powers/Player/Daredevil/WhirlingClubStaminaCostReductionCombo.prototype
        {   6521u, "Black Bolt" },  // Powers/Player/BlackBolt/BoltMissileEffect.prototype
        {   6524u, "Iron Man" },  // Powers/Player/IronMan/MissileCritPassiveEnduranceCost.prototype
        {   6526u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/FlashGrenadeWeakenCombo.prototype
        {   6527u, "Human Torch" },  // Powers/Player/HumanTorch/Traits/OverheatEffectTooHotToHitThorns.prototype
        {   6529u, "Nick Fury" },  // Powers/Player/NickFury/WarMachinePlasmaCannonAnimatedActor.prototype
        {   6531u, "Taskmaster" },  // Powers/Player/Taskmaster/SwordSlice5thHit.prototype
        {   6533u, "Punisher" },  // Powers/Player/Punisher/Rework/FlamethrowerHotspotEffect.prototype
        {   6537u, "Psylocke" },  // Powers/Player/Psylocke/PassiveDecoysSummonCombo.prototype
        {   6538u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/ChargeBeam.prototype
        {   6540u, "Moon Knight" },  // Powers/Player/MoonKnight/TumbleHasteCombo.prototype
        {   6541u, "Dr. Doom" },  // Powers/Player/DrDoom/DoomsDayFireworksEffect.prototype
        {   6543u, "Elektra" },  // Powers/Player/Elektra/BamfDiveBomb.prototype
        {   6545u, "Ghost Rider" },  // Powers/Player/GhostRider/HellfireCombustionCombo.prototype
        {   6546u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/StretchyBrainGadgetCDRProc.prototype
        {   6548u, "Cyclops" },  // Powers/Player/Cyclops/ChargingConeFullChargeStunCombo.prototype
        {   6550u, "Thor" },  // Powers/Player/Thor/Rework/BeserkerRage.prototype
        {   6552u, "Blade" },  // Powers/Player/Blade/AdvancdTechniqueEndTimerEarly.prototype
        {   6556u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent4CallInBuffsNightcrawler.prototype
        {   6559u, "Nova" },  // Powers/Player/Nova/MegaPunch.prototype
        {   6560u, "Beast" },  // Powers/Player/Beast/CloseGapDamageCombo.prototype
        {   6563u, "Dr. Doom" },  // Powers/Player/DrDoom/RapidFire.prototype
        {   6566u, "Blade" },  // Powers/Player/Blade/SpecHighRiskSpiritRestoreProc.prototype
        {   6567u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/HawkeyeBoomerangArrowMissileEffect.prototype
        {   6576u, "Venom" },  // Powers/Player/Venom/IchorSpearMissileEffect.prototype
        {   6577u, "Winter Soldier" },  // Powers/Player/WinterSoldier/AssassinateExecuteCombo.prototype
        {   6578u, "Beast" },  // Powers/Player/Beast/SleepGasGadgetExplosionEffect.prototype
        {   6579u, "Daredevil" },  // Powers/Player/Daredevil/Update/ConeYank.prototype
        {   6581u, "Loki" },  // Powers/Player/Loki/MagicChains.prototype
        {   6583u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PsylockeLungeRestore.prototype
        {   6586u, "Ant-Man" },  // Powers/Player/AntMan/BounceDashEffect.prototype
        {   6588u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateBattleVanRearHotspot.prototype
        {   6591u, "Colossus" },  // Powers/Player/Colossus/MagikEldritchArmor.prototype
        {   6593u, "Venom" },  // Powers/Player/Venom/SymbioteSummonAttack1.prototype
        {   6596u, "Iron Fist" },  // Powers/Player/IronFist/Pummel4thAttack.prototype
        {   6597u, "Ant-Man" },  // Powers/Player/AntMan/GiantManFootKeywordConditionCombo.prototype
        {   6604u, "Vision" },  // Powers/Player/Vision/AtomicFootDive.prototype
        {   6606u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/ArcTurret.prototype
        {   6607u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PullUnderCritBonusBuff.prototype
        {   6608u, "Iceman" },  // Powers/Player/Iceman/UltimateCCImmuneCombo.prototype
        {   6609u, "War Machine" },  // Powers/Player/WarMachine/ChaingunFullAutoMissileEffectPlasma.prototype
        {   6610u, "Black Widow" },  // Powers/Player/BlackWidow/TumbleChargeRegen.prototype
        {   6611u, "Angela" },  // Powers/Player/Angela/SwordPummel6thAttack.prototype
        {   6619u, "Iceman" },  // Powers/Player/Iceman/DeathFromAbove.prototype
        {   6621u, "Captain America" },  // Powers/Player/CaptainAmerica/BroadStrike.prototype
        {   6623u, "Black Bolt" },  // Powers/Player/BlackBolt/SuddenBeam.prototype
        {   6625u, "Doctor Strange" },  // Powers/Player/DoctorStrange/ProjectionWindsOfWatoombKnockback.prototype
        {   6633u, "Black Bolt" },  // Powers/Player/BlackBolt/BarrierKnockbackCombo.prototype
        {   6636u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/StretchyBrainDeactivateGiantGunC.prototype
        {   6639u, "She-Hulk" },  // Powers/Player/SheHulk/UltimateBuffCombo.prototype
        {   6640u, "Gambit" },  // Powers/Player/Gambit/AceOfSpades.prototype
        {   6641u, "Punisher" },  // Powers/Player/Punisher/Talents/TacticalShotgun.prototype
        {   6645u, "Taskmaster" },  // Powers/Player/Taskmaster/UltimateConditionTimerStop.prototype
        {   6647u, "She-Hulk" },  // Powers/Player/SheHulk/CleanseProcEffect.prototype
        {   6649u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Talents/Talent5ImplosionBuff.prototype
        {   6653u, "Taskmaster" },  // Powers/Player/Taskmaster/TumbleStealth.prototype
        {   6655u, "Ultron" },  // Powers/Player/Ultron/Traits/MechanicTraitBandwidth.prototype
        {   6656u, "Luke Cage" },  // Powers/Player/LukeCage/PummelNoFinisher.prototype
        {   6657u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Talents/Talent5StealthBuffs.prototype
        {   6664u, "Black Panther" },  // Powers/Player/BlackPanther/SmokeScreenSpecClawsBuff.prototype
        {   6665u, "Ghost Rider" },  // Powers/Player/GhostRider/Talents/Talent5ChainFlechette.prototype
        {   6671u, "Blade" },  // Powers/Player/Blade/Traits/AmmoRegenEnd.prototype
        {   6672u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhasingPunchDashCombo.prototype
        {   6673u, "Silver Surfer" },  // Powers/Player/SilverSurfer/DisengageExtraBeam.prototype
        {   6677u, "Black Widow" },  // Powers/Player/BlackWidow/PlastiqueTaunt.prototype
        {   6678u, "Wolverine" },  // Powers/Player/Wolverine/Dunk.prototype
        {   6679u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/JessicaJones/KickCarHotspot.prototype
        {   6680u, "Black Widow" },  // Powers/Player/BlackWidow/TwilightInitiativeDoTStunCombo.prototype
        {   6683u, "Iron Man" },  // Powers/Player/IronMan/SystemRebootCooldownDisplay.prototype
        {   6684u, "Cable" },  // Powers/Player/Cable/KineticRepulsionCooldownDisplay.prototype
        {   6688u, "Dr. Doom" },  // Powers/Player/DrDoom/FingerLasersBuffDamageCombo.prototype
        {   6689u, "Rogue" },  // Powers/Player/Rogue/ExtremeDrainInstantKillNonBoss.prototype
        {   6690u, "Taskmaster" },  // Powers/Player/Taskmaster/BrutalStrike.prototype
        {   6694u, "Black Bolt" },  // Powers/Player/BlackBolt/DisableEnergyDecayProcEffect.prototype
        {   6697u, "Magik" },  // Powers/Player/Magik/Talents/SoulConeLayerSoulswordBuff.prototype
        {   6699u, "Blade" },  // Powers/Player/Blade/HemoglycerinGauntlet5thAttack.prototype
        {   6703u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ColossusMetalSkinRegenProcEffect.prototype
        {   6704u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/InvisibilityDamageMovementSpeedBuff5sec.prototype
        {   6707u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveCaptainAmerica.prototype
        {   6708u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelBuffsHiddenPassiveChange.prototype
        {   6710u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/TeleportPBAoE.prototype
        {   6711u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondHeartAsProc.prototype
        {   6712u, "Nick Fury" },  // Powers/Player/NickFury/BasicPistol.prototype
        {   6713u, "Cable" },  // Powers/Player/Cable/ParticleAcceleratorSecondExplosion.prototype
        {   6715u, "Ultron" },  // Powers/Player/Ultron/UltimateDebris.prototype
        {   6716u, "Ghost Rider" },  // Powers/Player/GhostRider/LoopChainWhirlwind.prototype
        {   6718u, "X-23" },  // Powers/Player/X23/Talents/Talent4SigTipleKickCoupDeGraceCharge.prototype
        {   6719u, "Moon Knight" },  // Powers/Player/MoonKnight/BasicStaffStrike.prototype
        {   6720u, "Hulk" },  // Powers/Player/Hulk/Traits/AngerDecayPreventer.prototype
        {   6721u, "Gambit" },  // Powers/Player/Gambit/Talents/RaginCajunProcEffect.prototype
        {   6724u, "Hawkeye" },  // Powers/Player/Hawkeye/TurretArrowTrickArrowBuff.prototype
        {   6730u, "Magneto" },  // Powers/Player/Magneto/LungeEndCombo.prototype
        {   6731u, "Colossus" },  // Powers/Player/Colossus/PickUpTerrain.prototype
        {   6732u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/NastirhHealingShieldKnockbackCombo.prototype
        {   6733u, "Beast" },  // Powers/Player/Beast/Talents/Talent5SigJubilee.prototype
        {   6734u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GorgonStoneGaze.prototype
        {   6739u, "Nova" },  // Powers/Player/Nova/ChargedDashSummonCombo.prototype
        {   6740u, "Daredevil" },  // Powers/Player/Daredevil/Update/HealingTalentCombo.prototype
        {   6741u, "Angela" },  // Powers/Player/Angela/DoubleAxeThrowNonExecuteCombo.prototype
        {   6744u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/SevenRings.prototype
        {   6745u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BetaRayBillLightningBarrageBuff.prototype
        {   6746u, "Venom" },  // Powers/Player/Venom/SymbioteSummonPetHealthGainCombo.prototype
        {   6748u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GravityMine.prototype
        {   6753u, "Human Torch" },  // Powers/Player/HumanTorch/HomingShotEffect.prototype
        {   6754u, "Beast" },  // Powers/Player/Beast/TetherballPBAoEHotspotEffect.prototype
        {   6755u, "Thing" },  // Powers/Player/Thing/UltimateSelfBuffEffect.prototype
        {   6761u, "War Machine" },  // Powers/Player/WarMachine/BulletOneOffEffect.prototype
        {   6762u, "Dr. Doom" },  // Powers/Player/DrDoom/ForceFieldCooldownDisplay.prototype
        {   6764u, "War Machine" },  // Powers/Player/WarMachine/MissilePodsMissileEffect.prototype
        {   6765u, "Psylocke" },  // Powers/Player/Psylocke/LungePsiEnergyGain.prototype
        {   6767u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/RangedSquirrelAoEDamageEffect.prototype
        {   6768u, "Magik" },  // Powers/Player/Magik/DarkPactBFLDBuff.prototype
        {   6773u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/MicrodronesSecondWave.prototype
        {   6774u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaoticDebuffDoT.prototype
        {   6775u, "Taskmaster" },  // Powers/Player/Taskmaster/SwordSlice1stHit.prototype
        {   6776u, "Rogue" },  // Powers/Player/Rogue/Talents/StolenPowersBuff.prototype
        {   6781u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceVisualManager.prototype
        {   6782u, "Iron Man" },  // Powers/Player/IronMan/OrbitalBombardmentHotspotEffect.prototype
        {   6783u, "Ultron" },  // Powers/Player/Ultron/UltimateHotspotEffect.prototype
        {   6786u, "Ghost Rider" },  // Powers/Player/GhostRider/PassiveTeamDefenseHiddenPassive.prototype
        {   6787u, "Iron Fist" },  // Powers/Player/IronFist/SingleStanceMasteryStackRemoval.prototype
        {   6788u, "Venom" },  // Powers/Player/Venom/Ultimate.prototype
        {   6792u, "Black Panther" },  // Powers/Player/BlackPanther/SummonDoraSecondComboShort.prototype
        {   6793u, "Black Cat" },  // Powers/Player/BlackCat/TrapActivation.prototype
        {   6798u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/TumbleKineticBattery.prototype
        {   6802u, "X-23" },  // Powers/Player/X23/BladeSpinHotspotKnockback.prototype
        {   6804u, "Nova" },  // Powers/Player/Nova/PassiveWorldmindEndurGainProcEff.prototype
        {   6806u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/RemoveGunTurretSummons.prototype
        {   6809u, "Ant-Man" },  // Powers/Player/AntMan/GiantManFootGrowConditionEffect.prototype
        {   6812u, "Luke Cage" },  // Powers/Player/LukeCage/ChunkOConcreteMissileEffectFinisher.prototype
        {   6813u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCardMissileEffectStage3.prototype
        {   6815u, "Luke Cage" },  // Powers/Player/LukeCage/LukeCageUltimateEffectPets.prototype
        {   6820u, "Cyclops" },  // Powers/Player/Cyclops/Rework/BasicBouncingBeamChainEffect.prototype
        {   6821u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Groot.prototype
        {   6822u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveSpiderGwen.prototype
        {   6823u, "Wolverine" },  // Powers/Player/Wolverine/RunThroughCDReset.prototype
        {   6832u, "Luke Cage" },  // Powers/Player/LukeCage/ElbowDrop.prototype
        {   6833u, "Ultron" },  // Powers/Player/Ultron/SpinAttackVulnCombo.prototype
        {   6834u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Traits/OffenseTrait.prototype
        {   6837u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DamageConeDebuff.prototype
        {   6839u, "Doctor Strange" },  // Powers/Player/DoctorStrange/WindsOfWatoombKnockback.prototype
        {   6840u, "Cyclops" },  // Powers/Player/Cyclops/FocusBeamBuffPhase2Refresh.prototype
        {   6844u, "Winter Soldier" },  // Powers/Player/WinterSoldier/SniperShotCooldownReduce.prototype
        {   6845u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseDashMissileEffect.prototype
        {   6848u, "Hawkeye" },  // Powers/Player/Hawkeye/PoisonGasBombHotspotEffect.prototype
        {   6849u, "War Machine" },  // Powers/Player/WarMachine/ChainsawsBleedCombo.prototype
        {   6851u, "She-Hulk" },  // Powers/Player/SheHulk/Objection.prototype
        {   6852u, "Cyclops" },  // Powers/Player/Cyclops/FocusBeamPhase1Loop.prototype
        {   6858u, "Rogue" },  // Powers/Player/Rogue/UltimateDashSlash.prototype
        {   6859u, "Black Panther" },  // Powers/Player/BlackPanther/BasicDaggerThrowCostCombo.prototype
        {   6862u, "Magik" },  // Powers/Player/Magik/SoulShockwaveMissileEffect.prototype
        {   6863u, "War Machine" },  // Powers/Player/WarMachine/LifeSupportInvulnerabilityCombo.prototype
        {   6868u, "Blade" },  // Powers/Player/Blade/HemoglycerinGauntlet3rdAttack.prototype
        {   6870u, "Venom" },  // Powers/Player/Venom/BigPunch.prototype
        {   6875u, "Black Panther" },  // Powers/Player/BlackPanther/ClawUppercut.prototype
        {   6877u, "Jean Grey" },  // Powers/Player/JeanGrey/UltimateComboSecondaryRestore.prototype
        {   6882u, "Cable" },  // Powers/Player/Cable/VortexGrenadeHotspotEffect.prototype
        {   6893u, "Taskmaster" },  // Powers/Player/Taskmaster/SummonStudentCombo.prototype
        {   6900u, "Human Torch" },  // Powers/Player/HumanTorch/SummonFireWallHotspotEffect.prototype
        {   6902u, "Hulk" },  // Powers/Player/Hulk/Traits/DefenseTrait.prototype
        {   6904u, "Ant-Man" },  // Powers/Player/AntMan/FlyingAntSwarmLarge.prototype
        {   6907u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/IronFistTechniqueStackCount.prototype
        {   6908u, "Human Torch" },  // Powers/Player/HumanTorch/FlameOnExplosion.prototype
        {   6910u, "Doctor Strange" },  // Powers/Player/DoctorStrange/MysticEnergyOrbVisual5.prototype
        {   6911u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/PhoenixFormSpec.prototype
        {   6912u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Talents/Talent3WarpTurretBonus.prototype
        {   6913u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/Drain.prototype
        {   6915u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SilverSurferChanneledBeam.prototype
        {   6920u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DarkPhoenixForm.prototype
        {   6924u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/SpeedRushJeanProcEffect.prototype
        {   6926u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseToggleHealthOnPhaseProc.prototype
        {   6927u, "Loki" },  // Powers/Player/Loki/DecoyTurretsSorcerousBlastMEffec.prototype
        {   6930u, "War Machine" },  // Powers/Player/WarMachine/HeatHiddenPassive.prototype
        {   6931u, "Cable" },  // Powers/Player/Cable/TelepathicIllusionDoTProc.prototype
        {   6934u, "Magik" },  // Powers/Player/Magik/NastirhSummonCombo.prototype
        {   6938u, "Black Panther" },  // Powers/Player/BlackPanther/CleansePassiveSpiritProcEffect.prototype
        {   6943u, "Black Bolt" },  // Powers/Player/BlackBolt/HealthRegenBuffProcEffectConditionCancel.prototype
        {   6949u, "Silver Surfer" },  // Powers/Player/SilverSurfer/PowerCosmicGainMechanic.prototype
        {   6950u, "Loki" },  // Powers/Player/Loki/EnchantmentFire.prototype
        {   6951u, "Iron Fist" },  // Powers/Player/IronFist/Talents/Talent5DualStance.prototype
        {   6952u, "Elektra" },  // Powers/Player/Elektra/Talents/TripleChainCDRProc.prototype
        {   6953u, "Storm" },  // Powers/Player/Storm/ChanneledLightningAspdTooltip.prototype
        {   6954u, "Nick Fury" },  // Powers/Player/NickFury/Traits/DefaultAmmoRegenAsCombo.prototype
        {   6957u, "Magneto" },  // Powers/Player/Magneto/SpawnMetalOrbCombo.prototype
        {   6959u, "Moon Knight" },  // Powers/Player/MoonKnight/TumbleStunEffect.prototype
        {   6960u, "Magik" },  // Powers/Player/Magik/LifeTapDoT.prototype
        {   6961u, "Punisher" },  // Powers/Player/Punisher/Rework/ArmorPiercing.prototype
        {   6962u, "Magneto" },  // Powers/Player/Magneto/Talents/CrushConeBonus.prototype
        {   6963u, "Kitty Pryde" },  // Powers/Player/KittyPryde/SignatureLockheedBuff.prototype
        {   6969u, "War Machine" },  // Powers/Player/WarMachine/TeslaFieldHiddenPassive.prototype
        {   6971u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/NewSigHadronEnforcer.prototype
        {   6973u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BoardDashStackingBuff.prototype
        {   6974u, "Daredevil" },  // Powers/Player/Daredevil/Update/InvulnCombo.prototype
        {   6975u, "Beast" },  // Powers/Player/Beast/AngelDFA.prototype
        {   6976u, "War Machine" },  // Powers/Player/WarMachine/TearGasHotspotSlowEffect.prototype
        {   6983u, "Beast" },  // Powers/Player/Beast/Stomp.prototype
        {   6985u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/MicroNullifierBonusCDR.prototype
        {   6989u, "Loki" },  // Powers/Player/Loki/MindControlDoT.prototype
        {   6991u, "War Machine" },  // Powers/Player/WarMachine/UltimateSidekickCombo.prototype
        {   6994u, "Captain America" },  // Powers/Player/CaptainAmerica/TauntRapidHealing.prototype
        {   6995u, "Deadpool" },  // Powers/Player/Deadpool/UltimateVisualCombo.prototype
        {   6996u, "Rogue" },  // Powers/Player/Rogue/ResetSecondaryResource.prototype
        {   6997u, "Winter Soldier" },  // Powers/Player/WinterSoldier/UltimateHotspotEffect.prototype
        {   6998u, "Nick Fury" },  // Powers/Player/NickFury/Traits/DefaultAmmoBelow25PctEnd.prototype
        {   6999u, "Carnage" },  // Powers/Player/Carnage/Talents/BladeWeaponsSwordSpin.prototype
        {   7000u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ImplosionGadget.prototype
        {   7001u, "Venom" },  // Powers/Player/Venom/SigFreakoutDoTCombo.prototype
        {   7002u, "Iron Fist" },  // Powers/Player/IronFist/DragonSliceStance.prototype
        {   7008u, "Dr. Doom" },  // Powers/Player/DrDoom/MissilesMissileEffect.prototype
        {   7010u, "Kitty Pryde" },  // Powers/Player/KittyPryde/MovementSlashHitEffect.prototype
        {   7013u, "Thor" },  // Powers/Player/Thor/Talents/BruiserTalent.prototype
        {   7018u, "X-23" },  // Powers/Player/X23/BladeSpinComboSummon.prototype
        {   7019u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/NoUCBaseMissileEffect.prototype
        {   7021u, "Doctor Strange" },  // Powers/Player/DoctorStrange/BasicDaggersLocateProjection.prototype
        {   7022u, "Storm" },  // Powers/Player/Storm/ZephyrHotspotEffect.prototype
        {   7023u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/QuakeBeam.prototype
        {   7025u, "Emma Frost" },  // Powers/Player/EmmaFrost/UltimateSpiderRappel.prototype
        {   7028u, "Magneto" },  // Powers/Player/Magneto/Implosion.prototype
        {   7031u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/GunTurretBasicRifle.prototype
        {   7035u, "Angela" },  // Powers/Player/Angela/WhippingRibbonsPassive.prototype
        {   7037u, "Moon Knight" },  // Powers/Player/MoonKnight/NunchuckBulldozeRestoreProcEffect.prototype
        {   7038u, "Black Panther" },  // Powers/Player/BlackPanther/FreezingDaggersBuffCombo.prototype
        {   7041u, "Venom" },  // Powers/Player/Venom/SigFreakoutInvisCombo.prototype
        {   7042u, "Thing" },  // Powers/Player/Thing/KnockdownChargeCombo.prototype
        {   7043u, "Iceman" },  // Powers/Player/Iceman/Talents/IcemanClones.prototype
        {   7045u, "Emma Frost" },  // Powers/Player/EmmaFrost/UltimateSpiderRappelEnd.prototype
        {   7056u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ConeYankWeakenCombo.prototype
        {   7058u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ElectricAoEGadgetSummon.prototype
        {   7060u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SilverSurferChanneledBeamPhase2Loop.prototype
        {   7062u, "Cyclops" },  // Powers/Player/Cyclops/Rework/ConeBeamHiddenPassiveCDRProc.prototype
        {   7064u, "Emma Frost" },  // Powers/Player/EmmaFrost/UltimateTRexRoarKnockbackEffect.prototype
        {   7069u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/ConsumeProtectiveFlames.prototype
        {   7070u, "Blade" },  // Powers/Player/Blade/Talents/DFAInnerHitTalent.prototype
        {   7079u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CosmicWakeHotspotEffect.prototype
        {   7082u, "Beast" },  // Powers/Player/Beast/UltimateHotspotEffect.prototype
        {   7084u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotThumperCallInAsCombo.prototype
        {   7088u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/BouncyExpandingPBAOE.prototype
        {   7089u, "Human Torch" },  // Powers/Player/HumanTorch/FallbackBlastCombo.prototype
        {   7093u, "Cable" },  // Powers/Player/Cable/Talents/TKOverloadBuff.prototype
        {   7095u, "Venom" },  // Powers/Player/Venom/Talents/HealthRestoreBuff.prototype
        {   7098u, "Juggernaut" },  // Powers/Player/Juggernaut/UltimateHiddenPassive.prototype
        {   7099u, "Loki" },  // Powers/Player/Loki/Talents/MainSpecMelee.prototype
        {   7100u, "Venom" },  // Powers/Player/Venom/DefensePassive.prototype
        {   7101u, "Magneto" },  // Powers/Player/Magneto/UltimateHiddenPassive.prototype
        {   7102u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveAntmanOrbSummon.prototype
        {   7103u, "Loki" },  // Powers/Player/Loki/InfernalBinding.prototype
        {   7106u, "Luke Cage" },  // Powers/Player/LukeCage/Sprint.prototype
        {   7107u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Suffocate.prototype
        {   7108u, "Daredevil" },  // Powers/Player/Daredevil/ClubRicochetDestructibleKiller.prototype
        {   7109u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateStartImmobilizeInvuln.prototype
        {   7112u, "Hulk" },  // Powers/Player/Hulk/AvalancheLeapEnd.prototype
        {   7117u, "Wolverine" },  // Powers/Player/Wolverine/TornadoClawBuffed.prototype
        {   7118u, "Vision" },  // Powers/Player/Vision/SolarChanneledEnergyBeam.prototype
        {   7119u, "Hulk" },  // Powers/Player/Hulk/Traits/AngerDecayMechanic.prototype
        {   7121u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/DiveBomb.prototype
        {   7122u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Talents/Talent5DefBuffs.prototype
        {   7123u, "Hulk" },  // Powers/Player/Hulk/Rework/ThrowRock.prototype
        {   7126u, "Elektra" },  // Powers/Player/Elektra/CrossStrike.prototype
        {   7127u, "Angela" },  // Powers/Player/Angela/HevensWrathBuffs.prototype
        {   7131u, "Ultron" },  // Powers/Player/Ultron/BladeDroneSlash2.prototype
        {   7133u, "Psylocke" },  // Powers/Player/Psylocke/Crossbow.prototype
        {   7136u, "Deadpool" },  // Powers/Player/Deadpool/Rework/StrafeTalentBuff.prototype
        {   7144u, "Winter Soldier" },  // Powers/Player/WinterSoldier/RapidFireMissileEffect.prototype
        {   7146u, "Angela" },  // Powers/Player/Angela/DeathFromAbove.prototype
        {   7147u, "Elektra" },  // Powers/Player/Elektra/ShadowStrikeMovement.prototype
        {   7148u, "Psylocke" },  // Powers/Player/Psylocke/Lunge.prototype
        {   7156u, "Thor" },  // Powers/Player/Thor/Rework/StormHammerThrowSummonRemoval.prototype
        {   7160u, "Gambit" },  // Powers/Player/Gambit/BoWhirlwindRandomCard.prototype
        {   7164u, "Ultron" },  // Powers/Player/Ultron/RangeDroneRepulsorBeam2.prototype
        {   7168u, "Venom" },  // Powers/Player/Venom/Talents/BigPunchBuff.prototype
        {   7169u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/SeekerOrbs.prototype
        {   7171u, "Human Torch" },  // Powers/Player/HumanTorch/Traits/TriggerOverheat.prototype
        {   7173u, "Punisher" },  // Powers/Player/Punisher/Traits/DefaultAmmoRegenTrigger.prototype
        {   7174u, "Black Panther" },  // Powers/Player/BlackPanther/Talents/UnseenPredatorAutomaticTalent.prototype
        {   7178u, "Ultron" },  // Powers/Player/Ultron/MeleeDroneBasicPunch.prototype
        {   7180u, "Iron Man" },  // Powers/Player/IronMan/RepulsorBurstMissileEffect.prototype
        {   7187u, "War Machine" },  // Powers/Player/WarMachine/ChaingunBulletSpray.prototype
        {   7191u, "Thing" },  // Powers/Player/Thing/Rework/RemoveYancyStreetPowerAura.prototype
        {   7193u, "Hulk" },  // Powers/Player/Hulk/Rework/BasicMeleeUtilAngerGain.prototype
        {   7197u, "Daredevil" },  // Powers/Player/Daredevil/Ultimate.prototype
        {   7198u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallJeanObjectExplosionBurn.prototype
        {   7201u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Talents/Talent2SigItemProcs.prototype
        {   7202u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/RockTrollBerserkerSteroidHiddenPassive.prototype
        {   7203u, "Black Bolt" },  // Powers/Player/BlackBolt/Talents/Talent4SigCooldownReset.prototype
        {   7205u, "Iron Fist" },  // Powers/Player/IronFist/LeopardSlashEffect.prototype
        {   7206u, "Carnage" },  // Powers/Player/Carnage/Traits/SymbioteArmorDamageAbsorbStopperEnd.prototype
        {   7208u, "Gambit" },  // Powers/Player/Gambit/FoldEmKnockbackCombo.prototype
        {   7210u, "Nova" },  // Powers/Player/Nova/BasicSpiritBeamRestoreEffect.prototype
        {   7212u, "Venom" },  // Powers/Player/Venom/RangedBasicHealthGain.prototype
        {   7213u, "Captain America" },  // Powers/Player/CaptainAmerica/Talents/InspiredTeamBuffSpec.prototype
        {   7214u, "Taskmaster" },  // Powers/Player/Taskmaster/Ultimate.prototype
        {   7215u, "Magik" },  // Powers/Player/Magik/BFLDShockwave.prototype
        {   7216u, "Nightcrawler" },  // Powers/Player/Nightcrawler/FlourishDeflectBuffCombo.prototype
        {   7222u, "Black Bolt" },  // Powers/Player/BlackBolt/GapClose.prototype
        {   7223u, "X-23" },  // Powers/Player/X23/BladeSpinHealthOnHit.prototype
        {   7224u, "Magik" },  // Powers/Player/Magik/BoneSpiritAutoProc.prototype
        {   7227u, "Green Goblin" },  // Powers/Player/GreenGoblin/ElectricBlast.prototype
        {   7231u, "Iron Fist" },  // Powers/Player/IronFist/ShaolinStrike.prototype
        {   7234u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SuperSkrullWhirlwind.prototype
        {   7235u, "Gambit" },  // Powers/Player/Gambit/BasicKineticCardMissileEffect.prototype
        {   7237u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerLadyDeadpoolMeleeAttack4.prototype
        {   7238u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/MicroNullifierBonus.prototype
        {   7239u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateCallAngelEffect.prototype
        {   7240u, "Blade" },  // Powers/Player/Blade/Talents/BleedSlowTalent.prototype
        {   7241u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallIcemanSummonAoA.prototype
        {   7248u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/KirigiVanish.prototype
        {   7249u, "Punisher" },  // Powers/Player/Punisher/Talents/TriBarrelRPG.prototype
        {   7251u, "Rogue" },  // Powers/Player/Rogue/RogueBeastmodeCombo.prototype
        {   7252u, "Blade" },  // Powers/Player/Blade/LongLivedHealthOnHitProc.prototype
        {   7253u, "Carnage" },  // Powers/Player/Carnage/BasicClawsBladeStaffSecondHit4.prototype
        {   7254u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedFireballBurnDoT.prototype
        {   7255u, "Hulk" },  // Powers/Player/Hulk/Rework/ThrowRockComboBigger.prototype
        {   7256u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/OnslaughtSummonMentalOrb.prototype
        {   7260u, "X-23" },  // Powers/Player/X23/Talents/Talent3CrimsonLeapFuriousLungeCDR.prototype
        {   7261u, "Loki" },  // Powers/Player/Loki/ColdFront.prototype
        {   7262u, "Black Widow" },  // Powers/Player/BlackWidow/SniperShot.prototype
        {   7268u, "Doctor Strange" },  // Powers/Player/DoctorStrange/VishantiSealMindlessOneSummon.prototype
        {   7270u, "Green Goblin" },  // Powers/Player/GreenGoblin/GhostBomb.prototype
        {   7274u, "Storm" },  // Powers/Player/Storm/TyphoonSummonLocusCombo.prototype
        {   7275u, "Venom" },  // Powers/Player/Venom/Talents/SymbioteSpawns.prototype
        {   7282u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/UnlockPotentialProtectedBuff.prototype
        {   7283u, "Deadpool" },  // Powers/Player/Deadpool/Rework/BazookaExplosion.prototype
        {   7286u, "Carnage" },  // Powers/Player/Carnage/AxeSweep.prototype
        {   7289u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent1DamageMultWithNoArmorBuffEffect.prototype
        {   7290u, "Cable" },  // Powers/Player/Cable/TKOverloadBuffCombo.prototype
        {   7291u, "Magik" },  // Powers/Player/Magik/DestroyGreaterDemons.prototype
        {   7293u, "Nick Fury" },  // Powers/Player/NickFury/SniperShot.prototype
        {   7294u, "Moon Knight" },  // Powers/Player/MoonKnight/Tumble.prototype
        {   7296u, "Daredevil" },  // Powers/Player/Daredevil/Talents/ComboInvulnTalent.prototype
        {   7299u, "Black Bolt" },  // Powers/Player/BlackBolt/MasterBlowPunchCombo.prototype
        {   7303u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BasicStealthPunch.prototype
        {   7305u, "Elektra" },  // Powers/Player/Elektra/ThrowShuriken.prototype
        {   7308u, "Loki" },  // Powers/Player/Loki/UltimateNornStonesHotspotEffect.prototype
        {   7310u, "Luke Cage" },  // Powers/Player/LukeCage/PummelNoFinisher1stAttack.prototype
        {   7313u, "Psylocke" },  // Powers/Player/Psylocke/Butterflynado.prototype
        {   7316u, "Angela" },  // Powers/Player/Angela/Talents/WhippingRibbonsReflectTalent.prototype
        {   7319u, "Silver Surfer" },  // Powers/Player/SilverSurfer/PassiveOffenseProcEffect.prototype
        {   7320u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ArachneBouncingWebChainEffect.prototype
        {   7330u, "Winter Soldier" },  // Powers/Player/WinterSoldier/ArmSmash.prototype
        {   7334u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Talents/TeleportDodgeChanceTalent.prototype
        {   7342u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/BoomerangBubbleMissileEffect.prototype
        {   7347u, "Blade" },  // Powers/Player/Blade/Talents/SpecHighRisk.prototype
        {   7352u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedAutoAttackLockout1200MS.prototype
        {   7358u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Traits/ChaosOverloadEffect.prototype
        {   7360u, "Iron Fist" },  // Powers/Player/IronFist/Talents/Talent1StancePassiveBoost.prototype
        {   7361u, "X-23" },  // Powers/Player/X23/SigBladeDanceGate.prototype
        {   7362u, "Iceman" },  // Powers/Player/Iceman/IcemanFreezeCounterStackRemover.prototype
        {   7364u, "Taskmaster" },  // Powers/Player/Taskmaster/FuriousLunge.prototype
        {   7365u, "Psylocke" },  // Powers/Player/Psylocke/Traits/PsionicBarrierRegen.prototype
        {   7368u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallEmma.prototype
        {   7369u, "X-23" },  // Powers/Player/X23/BladeSpinIntervalHotspotEffect.prototype
        {   7370u, "Rogue" },  // Powers/Player/Rogue/UltimateSwordFlurryHotspotEffect.prototype
        {   7371u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/CleaSummonFlamesHotspotEffect.prototype
        {   7373u, "Beast" },  // Powers/Player/Beast/TeamworkSynergyEffect10s.prototype
        {   7377u, "Hulk" },  // Powers/Player/Hulk/Rework/Tantrum4thAttack.prototype
        {   7378u, "Psylocke" },  // Powers/Player/Psylocke/KatanaLeapSlashAoEMental.prototype
        {   7379u, "Iceman" },  // Powers/Player/Iceman/FuriousLungeCollide.prototype
        {   7383u, "Emma Frost" },  // Powers/Player/EmmaFrost/ControlFullHealComboEffect.prototype
        {   7386u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveThingClobberinTimeBuff.prototype
        {   7387u, "Moon Knight" },  // Powers/Player/MoonKnight/PassiveWhiteCostume.prototype
        {   7391u, "Hawkeye" },  // Powers/Player/Hawkeye/Kick.prototype
        {   7393u, "Hawkeye" },  // Powers/Player/Hawkeye/Talents/FlashBombTalent.prototype
        {   7394u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/LiftAndSlamPhoenixDamage.prototype
        {   7395u, "Iceman" },  // Powers/Player/Iceman/IcewallExplosion.prototype
        {   7398u, "Magik" },  // Powers/Player/Magik/TeleportPBAOE.prototype
        {   7399u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/MeleeSquirrelConeBonus.prototype
        {   7400u, "War Machine" },  // Powers/Player/WarMachine/WarMachineArmorStealth.prototype
        {   7401u, "Cable" },  // Powers/Player/Cable/Talents/VortexGrenadeLayer.prototype
        {   7402u, "Kitty Pryde" },  // Powers/Player/KittyPryde/UltimateHealProc.prototype
        {   7404u, "Cyclops" },  // Powers/Player/Cyclops/Rework/ChargingCone.prototype
        {   7409u, "Captain America" },  // Powers/Player/CaptainAmerica/BackwardsTumble.prototype
        {   7412u, "Beast" },  // Powers/Player/Beast/TeslaTowerGadgetLightningStrikeTwo.prototype
        {   7414u, "Magik" },  // Powers/Player/Magik/BFLDTauntCombo.prototype
        {   7417u, "Nightcrawler" },  // Powers/Player/Nightcrawler/TeleportBackstabPersistingStealth.prototype
        {   7421u, "Ghost Rider" },  // Powers/Player/GhostRider/ChargeUpBikeHotspots.prototype
        {   7422u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MrSinisterAstralProjection.prototype
        {   7425u, "Cyclops" },  // Powers/Player/Cyclops/CallMagikSummonCombo.prototype
        {   7426u, "Captain America" },  // Powers/Player/CaptainAmerica/BroadStrikeBlockBuff.prototype
        {   7427u, "Magneto" },  // Powers/Player/Magneto/SignatureMaelstromHotspotEffect.prototype
        {   7432u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ImplosionGadgetKnockdownEffect.prototype
        {   7433u, "Captain America" },  // Powers/Player/CaptainAmerica/UltimateHulkSmash.prototype
        {   7434u, "X-23" },  // Powers/Player/X23/PassiveStealthCDR.prototype
        {   7437u, "Gambit" },  // Powers/Player/Gambit/CardPickupBuffCombo.prototype
        {   7439u, "Gambit" },  // Powers/Player/Gambit/BasicBoStrikeEnduranceGainCombo.prototype
        {   7440u, "Hawkeye" },  // Powers/Player/Hawkeye/Ultimate.prototype
        {   7441u, "War Machine" },  // Powers/Player/WarMachine/ThermalShotHeatSpendCombo.prototype
        {   7444u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexSphere.prototype
        {   7445u, "Doctor Strange" },  // Powers/Player/DoctorStrange/VishantiSealMindlessOneEyeBlast.prototype
        {   7447u, "Iron Man" },  // Powers/Player/IronMan/SystemRebootKnockback.prototype
        {   7450u, "Taskmaster" },  // Powers/Player/Taskmaster/DiveKickEnd.prototype
        {   7452u, "Thing" },  // Powers/Player/Thing/Talents/Talent1CallInNoUseBenefit.prototype
        {   7454u, "Loki" },  // Powers/Player/Loki/MeddlingStrikeIllusionSummonCombo.prototype
        {   7456u, "Human Torch" },  // Powers/Player/HumanTorch/FlameTornadoLarger.prototype
        {   7457u, "Magik" },  // Powers/Player/Magik/SoulCaptureMinionDoT.prototype
        {   7459u, "Ant-Man" },  // Powers/Player/AntMan/BounceDash.prototype
        {   7460u, "Nova" },  // Powers/Player/Nova/PulsarProximityBuffHotspotEffect.prototype
        {   7464u, "Nick Fury" },  // Powers/Player/NickFury/ExecuteHit.prototype
        {   7465u, "Ultron" },  // Powers/Player/Ultron/RepairProtocolDroneHealingHotspotEffect.prototype
        {   7467u, "Green Goblin" },  // Powers/Player/GreenGoblin/GoblinSerumReviveCooldownDisplay.prototype
        {   7470u, "Psylocke" },  // Powers/Player/Psylocke/KatanaDoubleStrikeMental.prototype
        {   7476u, "Blade" },  // Powers/Player/Blade/HandCannon.prototype
        {   7477u, "Iceman" },  // Powers/Player/Iceman/DeathFromAboveEnd.prototype
        {   7479u, "Thing" },  // Powers/Player/Thing/CallOutEnemiesProcEffect.prototype
        {   7481u, "Black Cat" },  // Powers/Player/BlackCat/Ultimate.prototype
        {   7487u, "Blade" },  // Powers/Player/Blade/AdvancedTechniqueCombo1Stealth.prototype
        {   7490u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveSabretooth.prototype
        {   7494u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedChannelFireHotspotEffect.prototype
        {   7495u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HavokConeShot.prototype
        {   7498u, "Winter Soldier" },  // Powers/Player/WinterSoldier/ArmSmashMeleeBuffCombo.prototype
        {   7499u, "Magneto" },  // Powers/Player/Magneto/Talents/AutoDebrisShieldProcEffect.prototype
        {   7501u, "Storm" },  // Powers/Player/Storm/Talents/LightningSpec.prototype
        {   7502u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Talents/Talent4HadronEnforcerCharges.prototype
        {   7503u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickQuiverComboPoisonGasBomb.prototype
        {   7506u, "X-23" },  // Powers/Player/X23/Talents/Talent5DodgeBuffs.prototype
        {   7508u, "Angela" },  // Powers/Player/Angela/Talents/WhippingRibbonsSpeedTalent.prototype
        {   7510u, "Psylocke" },  // Powers/Player/Psylocke/PsiKatanaConeRangedDoT.prototype
        {   7515u, "Deadpool" },  // Powers/Player/Deadpool/Talents/BazookaTalent.prototype
        {   7516u, "Hulk" },  // Powers/Player/Hulk/Ultimate.prototype
        {   7517u, "Venom" },  // Powers/Player/Venom/PBAoEBlobHiddenPassive.prototype
        {   7519u, "Human Torch" },  // Powers/Player/HumanTorch/Consume.prototype
        {   7520u, "Nova" },  // Powers/Player/Nova/MegaPunchPulsarKill.prototype
        {   7522u, "Cyclops" },  // Powers/Player/Cyclops/Rework/TacticalAnalysis.prototype
        {   7523u, "Colossus" },  // Powers/Player/Colossus/GroupTaunt.prototype
        {   7525u, "Thor" },  // Powers/Player/Thor/ImmortalCombatRestoreFlatSpiritGain.prototype
        {   7527u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent5LawyerUpUnbreakable.prototype
        {   7529u, "Rogue" },  // Powers/Player/Rogue/Traits/OffenseTrait.prototype
        {   7532u, "Black Widow" },  // Powers/Player/BlackWidow/RollingGrenades.prototype
        {   7533u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ChanneledBeamBuffPhase2Start.prototype
        {   7547u, "Black Panther" },  // Powers/Player/BlackPanther/TripleShotKeywordConditionCombo.prototype
        {   7558u, "X-23" },  // Powers/Player/X23/WrathOnMovementUse.prototype
        {   7562u, "Deadpool" },  // Powers/Player/Deadpool/Rework/StrafeMeleeComboSummon.prototype
        {   7563u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/ControlledMobHiddenPassive.prototype
        {   7564u, "Carnage" },  // Powers/Player/Carnage/Traits/MechanicTraitSymbioteArmor.prototype
        {   7565u, "Deadpool" },  // Powers/Player/Deadpool/Ultimate.prototype
        {   7566u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/WidowsBootSpec.prototype
        {   7567u, "Vision" },  // Powers/Player/Vision/ModeToggle.prototype
        {   7569u, "Loki" },  // Powers/Player/Loki/IllusionRushDecoyPowerCollide.prototype
        {   7571u, "Hulk" },  // Powers/Player/Hulk/CrushingLeapEnd.prototype
        {   7573u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MoleManMoloidLeaperLeapAttackEnd.prototype
        {   7581u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondHeartDiamondComboTaunt.prototype
        {   7583u, "Blade" },  // Powers/Player/Blade/SerumInjectionHiddenPassive.prototype
        {   7587u, "Kitty Pryde" },  // Powers/Player/KittyPryde/STSSSlashBleed.prototype
        {   7592u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceCostComboSmall.prototype
        {   7596u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/FightingFocus.prototype
        {   7599u, "Black Widow" },  // Powers/Player/BlackWidow/CoupDeGraceNoBreakStealth.prototype
        {   7602u, "Colossus" },  // Powers/Player/Colossus/SiberianExpressComboSummon.prototype
        {   7603u, "Thing" },  // Powers/Player/Thing/Rework/RockyPunch.prototype
        {   7606u, "Nick Fury" },  // Powers/Player/NickFury/RapidFire.prototype
        {   7608u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHoleInstagibMobEffectInstantKill.prototype
        {   7615u, "Blade" },  // Powers/Player/Blade/AllOutAssault.prototype
        {   7618u, "Venom" },  // Powers/Player/Venom/HealthPassiveCooldownDisplay.prototype
        {   7619u, "Colossus" },  // Powers/Player/Colossus/UltimateComboTeamBuff.prototype
        {   7620u, "X-23" },  // Powers/Player/X23/SignatureTranceComboEnhanced.prototype
        {   7621u, "Black Widow" },  // Powers/Player/BlackWidow/SniperShotCDResetProcEffect.prototype
        {   7622u, "Nick Fury" },  // Powers/Player/NickFury/DriveBySummonVulnerability.prototype
        {   7623u, "Vision" },  // Powers/Player/Vision/ControlRobotHPassiveBuff.prototype
        {   7626u, "Venom" },  // Powers/Player/Venom/HealthPassiveHealProc.prototype
        {   7627u, "Captain America" },  // Powers/Player/CaptainAmerica/ShieldThrowPassiveBuffStackShiel.prototype
        {   7628u, "Magik" },  // Powers/Player/Magik/SummonsActivatedDirectedPower.prototype
        {   7629u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerCableConcussionBlast.prototype
        {   7631u, "Hulk" },  // Powers/Player/Hulk/UltimateCombo.prototype
        {   7633u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCardStage4.prototype
        {   7634u, "Wolverine" },  // Powers/Player/Wolverine/TornadoClawChargeCounter.prototype
        {   7636u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/InvisibleWomanShieldedFistMissileEffect.prototype
        {   7638u, "Carnage" },  // Powers/Player/Carnage/AxeDFABuffProtectionGain.prototype
        {   7640u, "Emma Frost" },  // Powers/Player/EmmaFrost/Traits/DefenseTraitDiamondForm.prototype
        {   7642u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHoleHotspotEffect.prototype
        {   7644u, "Rogue" },  // Powers/Player/Rogue/DrainPunch.prototype
        {   7645u, "Juggernaut" },  // Powers/Player/Juggernaut/MomentumDecayAsCombo.prototype
        {   7646u, "Hulk" },  // Powers/Player/Hulk/Talents/CooldownReductionEffect.prototype
        {   7648u, "Jean Grey" },  // Powers/Player/JeanGrey/Traits/DarkPhoenixFormSwitchBackToJean.prototype
        {   7649u, "Ultron" },  // Powers/Player/Ultron/FingerLaserBlasts.prototype
        {   7651u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MedusaAutoSlap.prototype
        {   7652u, "Thor" },  // Powers/Player/Thor/Rework/KnockOut.prototype
        {   7657u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/SpikedBallChanneledKnockback.prototype
        {   7658u, "Carnage" },  // Powers/Player/Carnage/TransfusionHealthTransferCombo.prototype
        {   7660u, "Black Widow" },  // Powers/Player/BlackWidow/ElectricBatonsAspdTooltip.prototype
        {   7661u, "Daredevil" },  // Powers/Player/Daredevil/Traits/MechanicTraitComboPoints.prototype
        {   7662u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/BrutalChanceTerrify.prototype
        {   7663u, "Colossus" },  // Powers/Player/Colossus/NightcrawlerSummon/DefaultAttack4.prototype
        {   7665u, "Deadpool" },  // Powers/Player/Deadpool/Talents/SmellsLikeVictoryIncrement.prototype
        {   7668u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelAttackSummonAllSquirrelsExtraFive.prototype
        {   7670u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/GunTurretAtkSpd.prototype
        {   7671u, "Captain America" },  // Powers/Player/CaptainAmerica/Talents/Serum100PctSpenderSpec.prototype
        {   7672u, "Dr. Doom" },  // Powers/Player/DrDoom/Talents/Talent5AllMagic.prototype
        {   7673u, "Blade" },  // Powers/Player/Blade/Helichopter.prototype
        {   7674u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AmpControlledMobInvisibilityCombo.prototype
        {   7675u, "Cable" },  // Powers/Player/Cable/PsimitarLungeEffect.prototype
        {   7677u, "Luke Cage" },  // Powers/Player/LukeCage/ThrowCar.prototype
        {   7678u, "Storm" },  // Powers/Player/Storm/Talents/AllTempests.prototype
        {   7679u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexSphereNonChaos.prototype
        {   7684u, "Ghost Rider" },  // Powers/Player/GhostRider/FirePillarSummonCombo.prototype
        {   7690u, "Angela" },  // Powers/Player/Angela/SigNoMatchEnduranceGain.prototype
        {   7692u, "Taskmaster" },  // Powers/Player/Taskmaster/DisengagingShotCombo.prototype
        {   7693u, "Green Goblin" },  // Powers/Player/GreenGoblin/RocketsMissileEffect.prototype
        {   7694u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveDaredevilComboPointMechanic.prototype
        {   7695u, "Cyclops" },  // Powers/Player/Cyclops/Talents/MagikCallinTalent.prototype
        {   7696u, "Storm" },  // Powers/Player/Storm/LightningColumnStackingBuff.prototype
        {   7697u, "Magik" },  // Powers/Player/Magik/OtherwordlyNovaHealPvP.prototype
        {   7699u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CosmicGiftEndurance.prototype
        {   7701u, "Iron Man" },  // Powers/Player/IronMan/Traits/DefenseTrait.prototype
        {   7702u, "Punisher" },  // Powers/Player/Punisher/Traits/DefaultAmmoBelow25PctEnd.prototype
        {   7703u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/CleaSummonFlamesImplosion.prototype
        {   7706u, "Elektra" },  // Powers/Player/Elektra/BamfDiveBombHideMesh.prototype
        {   7708u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotBlockadeCallInPunch.prototype
        {   7710u, "Storm" },  // Powers/Player/Storm/MassiveLightningStrike.prototype
        {   7711u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexSphereOrbVisual0.prototype
        {   7720u, "Wolverine" },  // Powers/Player/Wolverine/SignatureDashSlashBuffCombo.prototype
        {   7721u, "Angela" },  // Powers/Player/Angela/HackSlashEnduranceGain.prototype
        {   7723u, "Black Bolt" },  // Powers/Player/BlackBolt/Talents/Talent1DeathFromAboveAuraHotspot.prototype
        {   7730u, "Iceman" },  // Powers/Player/Iceman/Talents/IceGolemBuff.prototype
        {   7731u, "Doctor Strange" },  // Powers/Player/DoctorStrange/AstralFormAutoCombo.prototype
        {   7732u, "Ghost Rider" },  // Powers/Player/GhostRider/PassiveRegenInvulnerableEffect.prototype
        {   7739u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveRescue.prototype
        {   7743u, "Black Bolt" },  // Powers/Player/BlackBolt/MasterBlowSignatureAoEDamage.prototype
        {   7744u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickVolley.prototype
        {   7745u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/CyttorakPowersShareCD.prototype
        {   7751u, "Kitty Pryde" },  // Powers/Player/KittyPryde/UltimateHotspotEffect.prototype
        {   7752u, "Rogue" },  // Powers/Player/Rogue/UltimateDashSlash5.prototype
        {   7753u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosBlastBuffFromDarkHex.prototype
        {   7755u, "Gambit" },  // Powers/Player/Gambit/UltimateCardThrow5.prototype
        {   7756u, "Iron Man" },  // Powers/Player/IronMan/UnibeamUpgradedDamageCombo.prototype
        {   7757u, "Iron Fist" },  // Powers/Player/IronFist/ChiSteroidCooldownReductionBuff.prototype
        {   7759u, "Black Bolt" },  // Powers/Player/BlackBolt/Talent1EnergyPassiveMeleeBuff.prototype
        {   7761u, "Iceman" },  // Powers/Player/Iceman/Chill.prototype
        {   7762u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseAoEStopCombo.prototype
        {   7763u, "Elektra" },  // Powers/Player/Elektra/AssassinateComboElite.prototype
        {   7766u, "Loki" },  // Powers/Player/Loki/Traits/OffenseTrait.prototype
        {   7768u, "Dr. Doom" },  // Powers/Player/DrDoom/Traits/OffenseTrait.prototype
        {   7769u, "Iceman" },  // Powers/Player/Iceman/AbsoluteZeroGolemBuff.prototype
        {   7774u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CosmicGiftToughness.prototype
        {   7776u, "Jean Grey" },  // Powers/Player/JeanGrey/Traits/OffenseTrait.prototype
        {   7778u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerPirateDeadpoolDefaultAttack.prototype
        {   7782u, "Punisher" },  // Powers/Player/Punisher/Rework/Reload.prototype
        {   7786u, "Iceman" },  // Powers/Player/Iceman/Talents/MeleeWeapons.prototype
        {   7787u, "Venom" },  // Powers/Player/Venom/BigWebShootIchorGain.prototype
        {   7788u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/UltimateMissileLauncherMissileEf.prototype
        {   7790u, "Elektra" },  // Powers/Player/Elektra/StaffStrikeCastSpeedBuff.prototype
        {   7793u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent4CallInBuffs.prototype
        {   7797u, "Beast" },  // Powers/Player/Beast/FlyingBeatdown.prototype
        {   7802u, "Magik" },  // Powers/Player/Magik/OtherworldlyNova.prototype
        {   7805u, "Ghost Rider" },  // Powers/Player/GhostRider/RideBikeHotspots.prototype
        {   7806u, "Thing" },  // Powers/Player/Thing/Rework/YancyStreetGangMuggingProcEffect.prototype
        {   7807u, "Elektra" },  // Powers/Player/Elektra/SpinningStrike.prototype
        {   7809u, "Gambit" },  // Powers/Player/Gambit/BasicBoStrike.prototype
        {   7810u, "Ant-Man" },  // Powers/Player/AntMan/AntAllyCounterDown.prototype
        {   7811u, "Thing" },  // Powers/Player/Thing/BasicLineAoEPunchSpiritGainCombo.prototype
        {   7812u, "Daredevil" },  // Powers/Player/Daredevil/HealingTranceComboEffect.prototype
        {   7814u, "Beast" },  // Powers/Player/Beast/PreventMomentumDecay3s.prototype
        {   7816u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/UltimateNoMoreInvulnerableCombo.prototype
        {   7819u, "Cyclops" },  // Powers/Player/Cyclops/Talents/EmmaFrostCallInTalent.prototype
        {   7822u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveCoulson.prototype
        {   7825u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/RhinoBigChargeInstaKill.prototype
        {   7832u, "Nightcrawler" },  // Powers/Player/Nightcrawler/SwordPummel2ndAttack.prototype
        {   7833u, "Psylocke" },  // Powers/Player/Psylocke/KatanaPBAOEHideKatanaCombo.prototype
        {   7836u, "Iron Fist" },  // Powers/Player/IronFist/TigerClawSecondHit.prototype
        {   7837u, "Beast" },  // Powers/Player/Beast/Traits/DefenseTrait.prototype
        {   7838u, "Deadpool" },  // Powers/Player/Deadpool/Talents/SmellsLikeVictoryStackingBuff.prototype
        {   7841u, "Gambit" },  // Powers/Player/Gambit/UltimateCardThrow.prototype
        {   7843u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/UnmakeRealityCooldown.prototype
        {   7844u, "Elektra" },  // Powers/Player/Elektra/TripleChain3rdHit.prototype
        {   7846u, "Cyclops" },  // Powers/Player/Cyclops/Rework/ChanneledBeam.prototype
        {   7847u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/ForceFieldGeneratorDeathProc.prototype
        {   7852u, "War Machine" },  // Powers/Player/WarMachine/HeatGainPlasmaCannon.prototype
        {   7856u, "Iron Man" },  // Powers/Player/IronMan/MissileSalvo.prototype
        {   7857u, "She-Hulk" },  // Powers/Player/SheHulk/Talent1IncreaseFinisherDamageBuff.prototype
        {   7858u, "Wolverine" },  // Powers/Player/Wolverine/Talents/Talent1PassiveCombatFury.prototype
        {   7861u, "Thor" },  // Powers/Player/Thor/Rework/BoltSprayHotspotEffect.prototype
        {   7862u, "Cable" },  // Powers/Player/Cable/ConcussionBlast.prototype
        {   7863u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/WaspBiosprayHotspotEffect.prototype
        {   7865u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/HandClapFullMomentumSpender.prototype
        {   7870u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LokiIllusionRush.prototype
        {   7873u, "Luke Cage" },  // Powers/Player/LukeCage/YankComboPointGainEffect.prototype
        {   7875u, "Black Cat" },  // Powers/Player/BlackCat/Talents/TalentTrapsDontBreakStealth.prototype
        {   7876u, "Iron Man" },  // Powers/Player/IronMan/UltimateSuitBasicPunch2.prototype
        {   7877u, "Nova" },  // Powers/Player/Nova/FuriousLungeMoveSpeedBuff3.prototype
        {   7878u, "Psylocke" },  // Powers/Player/Psylocke/PsiBolt.prototype
        {   7879u, "Black Widow" },  // Powers/Player/BlackWidow/Traits/ChargeRegen.prototype
        {   7887u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent2NoCallinSpec.prototype
        {   7890u, "Dr. Doom" },  // Powers/Player/DrDoom/MagicOrbFillerAttack.prototype
        {   7891u, "Emma Frost" },  // Powers/Player/EmmaFrost/Traits/DiamondFormConditionThorns.prototype
        {   7893u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/SeekerOrbsBonus.prototype
        {   7894u, "Loki" },  // Powers/Player/Loki/SpiritsOfTheDead.prototype
        {   7896u, "Gambit" },  // Powers/Player/Gambit/CardPickupMissileEffect.prototype
        {   7898u, "Elektra" },  // Powers/Player/Elektra/Talents/StealthMarkTalent.prototype
        {   7899u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MsMarvelPhotonicWave.prototype
        {   7905u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BlackPantherSweepingKick.prototype
        {   7907u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveRocketRaccoon.prototype
        {   7909u, "Cyclops" },  // Powers/Player/Cyclops/FocusBeamTargetAudio.prototype
        {   7911u, "Ghost Rider" },  // Powers/Player/GhostRider/Talents/Talent1InfernalContract.prototype
        {   7920u, "Ghost Rider" },  // Powers/Player/GhostRider/UltimateForRealz.prototype
        {   7921u, "Taskmaster" },  // Powers/Player/Taskmaster/FreezeArrowHotspotEffect.prototype
        {   7923u, "Iceman" },  // Powers/Player/Iceman/IceGolemHaymaker.prototype
        {   7924u, "Iceman" },  // Powers/Player/Iceman/Talents/IcemanClonesSummonClone.prototype
        {   7927u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/BigPunch.prototype
        {   7928u, "Magneto" },  // Powers/Player/Magneto/Talents/AutoDebrisShieldProcRouter.prototype
        {   7930u, "Gambit" },  // Powers/Player/Gambit/Talents/BlackSuitBuff.prototype
        {   7934u, "Doctor Strange" },  // Powers/Player/DoctorStrange/TeleportPBAOE.prototype
        {   7936u, "Hulk" },  // Powers/Player/Hulk/AvalancheLeapEndKDCombo.prototype
        {   7938u, "Captain America" },  // Powers/Player/CaptainAmerica/SerumCooldownReductionProc.prototype
        {   7940u, "Ghost Rider" },  // Powers/Player/GhostRider/ChainRootMissileEffect.prototype
        {   7944u, "Captain America" },  // Powers/Player/CaptainAmerica/Traits/MechanicTraitSerum.prototype
        {   7948u, "Doctor Strange" },  // Powers/Player/DoctorStrange/BasicDaggers.prototype
        {   7956u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerPirateDeadpoolDefaultAttackMissileEff.prototype
        {   7957u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/FlashGrenade.prototype
        {   7960u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SecondaryResourceResetColossus.prototype
        {   7961u, "Loki" },  // Powers/Player/Loki/JotunFleshProcFilterPower.prototype
        {   7962u, "Human Torch" },  // Powers/Player/HumanTorch/BowlingBallHotspotEffect.prototype
        {   7963u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Traits/DefenseTrait.prototype
        {   7965u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GrootOutProcEffect.prototype
        {   7968u, "Silver Surfer" },  // Powers/Player/SilverSurfer/TimeWarp.prototype
        {   7970u, "Iceman" },  // Powers/Player/Iceman/FrozenOrbMovingDamageEffect.prototype
        {   7974u, "X-23" },  // Powers/Player/X23/TumbleHasteCombo.prototype
        {   7975u, "Taskmaster" },  // Powers/Player/Taskmaster/SteroidHotspot.prototype
        {   7977u, "Loki" },  // Powers/Player/Loki/IllusionRushDecoyPower.prototype
        {   7980u, "She-Hulk" },  // Powers/Player/SheHulk/MoveToStrikeComboPointGain.prototype
        {   7982u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/ThingLampBatThrowMissileEffect.prototype
        {   7983u, "Magik" },  // Powers/Player/Magik/DarkPactSpitterBuff.prototype
        {   7985u, "Storm" },  // Powers/Player/Storm/HurricaneWindsKnockback.prototype
        {   7987u, "Thing" },  // Powers/Player/Thing/Rework/Knockout.prototype
        {   7988u, "Nova" },  // Powers/Player/Nova/Talents/Talent4PulsarFastDetonation.prototype
        {   7990u, "Luke Cage" },  // Powers/Player/LukeCage/SweetChristmasSelfCooldownReset.prototype
        {   7993u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent4MovementBuildBuffsCDR.prototype
        {   7994u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/SignatureTributeGain.prototype
        {   7996u, "Hawkeye" },  // Powers/Player/Hawkeye/KickMeleeBonus.prototype
        {   8000u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ObfuscationInvulnerability.prototype
        {   8002u, "Thing" },  // Powers/Player/Thing/Rework/Knockout2ndAttack.prototype
        {   8003u, "Ant-Man" },  // Powers/Player/AntMan/InsectDecoyTaunt.prototype
        {   8004u, "Taskmaster" },  // Powers/Player/Taskmaster/BasicShotMissileEffect.prototype
        {   8008u, "Cable" },  // Powers/Player/Cable/PsimitarLungeSummonCombo.prototype
        {   8009u, "Cyclops" },  // Powers/Player/Cyclops/FocusBeamPhase2Loop.prototype
        {   8011u, "Thing" },  // Powers/Player/Thing/CallSuziePassive.prototype
        {   8012u, "X-23" },  // Powers/Player/X23/GrievousWounds360AoE200.prototype
        {   8017u, "Luke Cage" },  // Powers/Player/LukeCage/ChunkOConcreteFinisher.prototype
        {   8018u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MagikSummonDemons.prototype
        {   8019u, "Iron Man" },  // Powers/Player/IronMan/Talents/UnstableCore.prototype
        {   8020u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Traits/MechanicTraitSquirrels.prototype
        {   8025u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MagnetoAllIn.prototype
        {   8027u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HellfireDoTAura.prototype
        {   8029u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelBombs.prototype
        {   8030u, "Human Torch" },  // Powers/Player/HumanTorch/UltimateInvulnCombo.prototype
        {   8032u, "Ultron" },  // Powers/Player/Ultron/BladeDroneSlash.prototype
        {   8036u, "Blade" },  // Powers/Player/Blade/Talents/SigSpiritRestoreTalent.prototype
        {   8037u, "She-Hulk" },  // Powers/Player/SheHulk/Assault.prototype
        {   8038u, "Loki" },  // Powers/Player/Loki/FrostArmorHealProc.prototype
        {   8045u, "X-23" },  // Powers/Player/X23/DodgeOnMovementBuff.prototype
        {   8046u, "Loki" },  // Powers/Player/Loki/Talents/FourRealmsLightColumn.prototype
        {   8048u, "Punisher" },  // Powers/Player/Punisher/Rework/Magnum.prototype
        {   8050u, "Moon Knight" },  // Powers/Player/MoonKnight/CestusGauntletPunchDoT.prototype
        {   8051u, "Moon Knight" },  // Powers/Player/MoonKnight/BrutalChanceTerrifyProcEffect.prototype
        {   8053u, "Black Bolt" },  // Powers/Player/BlackBolt/Geyser.prototype
        {   8054u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/UltimateDeathFromAboveEnd.prototype
        {   8055u, "Gambit" },  // Powers/Player/Gambit/StreetSweepHotspots.prototype
        {   8061u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/QuicksilverSupersonicCycloneMovespeedBuff.prototype
        {   8065u, "Iron Man" },  // Powers/Player/IronMan/UltimateSuitWristRocket.prototype
        {   8067u, "Nova" },  // Powers/Player/Nova/PassiveWorldmind.prototype
        {   8068u, "Luke Cage" },  // Powers/Player/LukeCage/PunchTheGroundNoCharge.prototype
        {   8069u, "Carnage" },  // Powers/Player/Carnage/BasicClawsClawWasUsedLastAsProc.prototype
        {   8070u, "Iron Fist" },  // Powers/Player/IronFist/ShaolinMasterHiddenPassive.prototype
        {   8073u, "Blade" },  // Powers/Player/Blade/SerumInjectionHighRiskBloodlustBuffs.prototype
        {   8075u, "Ant-Man" },  // Powers/Player/AntMan/ShrinkEscapeHiddenPassive.prototype
        {   8076u, "Nova" },  // Powers/Player/Nova/PassiveSRShieldHiddenPassive.prototype
        {   8077u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/PassiveShieldHiddenPassive.prototype
        {   8079u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MrFantasticConeRapidPunchSlow.prototype
        {   8080u, "Loki" },  // Powers/Player/Loki/Traits/MechanicTraitIllusions.prototype
        {   8085u, "Thor" },  // Powers/Player/Thor/BallLightningHammerArc.prototype
        {   8087u, "Juggernaut" },  // Powers/Player/Juggernaut/HeadbuttBleedCombo.prototype
        {   8088u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/JessicaJones/JessicaJonesDeathFromAboveCombo.prototype
        {   8089u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SilverSurferChanneledBeamPhase1Loop.prototype
        {   8091u, "Thor" },  // Powers/Player/Thor/HammerPushSecondarySlow.prototype
        {   8095u, "Iron Man" },  // Powers/Player/IronMan/FreonRaySlow.prototype
        {   8097u, "Iron Fist" },  // Powers/Player/IronFist/PummelStartCombo.prototype
        {   8099u, "Angela" },  // Powers/Player/Angela/SwordPummel.prototype
        {   8100u, "Blade" },  // Powers/Player/Blade/HemoglycerinGauntletBleedVulnerability.prototype
        {   8102u, "Nightcrawler" },  // Powers/Player/Nightcrawler/SwordPummelFinalAttack.prototype
        {   8103u, "Punisher" },  // Powers/Player/Punisher/Rework/BulletSpray.prototype
        {   8108u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ColossusMetalSkin.prototype
        {   8110u, "Daredevil" },  // Powers/Player/Daredevil/Traits/DefenseTrait.prototype
        {   8112u, "Vision" },  // Powers/Player/Vision/PhasingModeUltraLight.prototype
        {   8113u, "She-Hulk" },  // Powers/Player/SheHulk/BarristerBeatdown8thHit.prototype
        {   8114u, "Storm" },  // Powers/Player/Storm/StormSurgeFreezeCombo.prototype
        {   8116u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseAOEWeakenCombo.prototype
        {   8118u, "Rogue" },  // Powers/Player/Rogue/StolenPowerLibrarySlot4.prototype
        {   8119u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/Obfuscation.prototype
        {   8121u, "Nick Fury" },  // Powers/Player/NickFury/MicroDronesRandomPositionLongerHit.prototype
        {   8122u, "Wolverine" },  // Powers/Player/Wolverine/FuryGain.prototype
        {   8127u, "Blade" },  // Powers/Player/Blade/JustStayDownImmunity.prototype
        {   8129u, "Vision" },  // Powers/Player/Vision/PhasePunchDodgeDamageReductionBonus.prototype
        {   8131u, "Human Torch" },  // Powers/Player/HumanTorch/ChanneledEnergyBeam.prototype
        {   8132u, "Blade" },  // Powers/Player/Blade/BloodlustMaxedNEW.prototype
        {   8136u, "Ultron" },  // Powers/Player/Ultron/ConcussionBlastBig.prototype
        {   8137u, "Vision" },  // Powers/Player/Vision/DeathFromBelowDamageShieldCombo.prototype
        {   8138u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/MovementPowersElbowDropBuff.prototype
        {   8140u, "Hawkeye" },  // Powers/Player/Hawkeye/Talents/TridentArrowheadsTalent.prototype
        {   8142u, "Moon Knight" },  // Powers/Player/MoonKnight/SummonKhonshuStatueBuffAura.prototype
        {   8144u, "Rogue" },  // Powers/Player/Rogue/UltimateMetalRegenerationCombo.prototype
        {   8147u, "Punisher" },  // Powers/Player/Punisher/Rework/Shotgun.prototype
        {   8148u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/BasicTripleSquirrel.prototype
        {   8150u, "Cable" },  // Powers/Player/Cable/TKSpearSlamDamageShieldCombo.prototype
        {   8157u, "Black Widow" },  // Powers/Player/BlackWidow/WidowsBiteMissileCombo.prototype
        {   8160u, "Wolverine" },  // Powers/Player/Wolverine/LungeProcEffect.prototype
        {   8161u, "Human Torch" },  // Powers/Player/HumanTorch/BouncingFireballsMissileEffectDoT.prototype
        {   8162u, "Ultron" },  // Powers/Player/Ultron/SummonSignatureHotspot.prototype
        {   8166u, "Storm" },  // Powers/Player/Storm/UltimateBuffEffect.prototype
        {   8170u, "Vision" },  // Powers/Player/Vision/SolarBoltSolarEnergyBonus.prototype
        {   8171u, "Dr. Doom" },  // Powers/Player/DrDoom/GroundSmashCostReductionBuff.prototype
        {   8173u, "Blade" },  // Powers/Player/Blade/KnifeBarrageMissileEffect.prototype
        {   8175u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/RobbieReyesDriveByHotspotEffect.prototype
        {   8179u, "Iron Fist" },  // Powers/Player/IronFist/Talents/Talent1OpenerDamageMult.prototype
        {   8181u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerDeadpoolTheKidDefaultAttackMissileEff.prototype
        {   8182u, "Ultron" },  // Powers/Player/Ultron/RangeDronePulseBoltEffect.prototype
        {   8184u, "Hawkeye" },  // Powers/Player/Hawkeye/PymSpiritAndHealthOnHit.prototype
        {   8185u, "Gambit" },  // Powers/Player/Gambit/FoldEmEnd.prototype
        {   8186u, "X-23" },  // Powers/Player/X23/SigBladeDance.prototype
        {   8196u, "Nick Fury" },  // Powers/Player/NickFury/Talents/Talent1FireteamRifles.prototype
        {   8197u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Talents/Talent2PhaseDashDoT.prototype
        {   8198u, "Magik" },  // Powers/Player/Magik/LimboDemonSummonCombo.prototype
        {   8213u, "Taskmaster" },  // Powers/Player/Taskmaster/ShieldBounce.prototype
        {   8214u, "Loki" },  // Powers/Player/Loki/EternalDarknessStunCombo.prototype
        {   8220u, "Rogue" },  // Powers/Player/Rogue/DrainLifeStealPower.prototype
        {   8221u, "Hulk" },  // Powers/Player/Hulk/Rework/WorldBreaker.prototype
        {   8222u, "Angela" },  // Powers/Player/Angela/RibbonDancer.prototype
        {   8223u, "Iron Fist" },  // Powers/Player/IronFist/ChiMasteryAsCombo.prototype
        {   8225u, "Rogue" },  // Powers/Player/Rogue/Talents/RecallOverloadCooldown.prototype
        {   8227u, "Black Bolt" },  // Powers/Player/BlackBolt/HypersonicScreamMobEffect.prototype
        {   8233u, "Gambit" },  // Powers/Player/Gambit/CheatDeathCooldownDisplay.prototype
        {   8235u, "Human Torch" },  // Powers/Player/HumanTorch/SummonFireWall.prototype
        {   8236u, "Angela" },  // Powers/Player/Angela/Talents/WeaponsCooldownReductionTalent.prototype
        {   8239u, "Magik" },  // Powers/Player/Magik/LimboSpitterLobAttack.prototype
        {   8240u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/SignatureNukeMissileEffect.prototype
        {   8241u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Talents/TeleportDamageBuffTalent.prototype
        {   8246u, "Deadpool" },  // Powers/Player/Deadpool/Rework/StrafeMissileEffect.prototype
        {   8248u, "Juggernaut" },  // Powers/Player/Juggernaut/UltimateStartQuakes.prototype
        {   8250u, "Doctor Strange" },  // Powers/Player/DoctorStrange/DemonsOfDenakTransfer3.prototype
        {   8252u, "Rogue" },  // Powers/Player/Rogue/UltimateSwordFlurryStart.prototype
        {   8254u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ScarletWitchShadowBoltBuff.prototype
        {   8255u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/PBAoEKnockdownDodgeBuff.prototype
        {   8256u, "She-Hulk" },  // Powers/Player/SheHulk/LawyerUpHighlightStop.prototype
        {   8258u, "Venom" },  // Powers/Player/Venom/DefensePassiveCooldownDisplay.prototype
        {   8259u, "Vision" },  // Powers/Player/Vision/ModeToggleSwitchToPhaseShortBuff.prototype
        {   8262u, "Iceman" },  // Powers/Player/Iceman/FrozenOrbChill.prototype
        {   8264u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/NightcrawlerValiantLeapBounce.prototype
        {   8265u, "Elektra" },  // Powers/Player/Elektra/BamfDiveBombEnd.prototype
        {   8266u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HydeDirectedShockwavePBAOEVisual.prototype
        {   8267u, "Angela" },  // Powers/Player/Angela/DFASecondActivation.prototype
        {   8268u, "War Machine" },  // Powers/Player/WarMachine/WarMachineArmor.prototype
        {   8272u, "Winter Soldier" },  // Powers/Player/WinterSoldier/SpinningMines.prototype
        {   8273u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Talents/Talent4ArcTurretLightning.prototype
        {   8274u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MoleManMoloidHurlerThrow.prototype
        {   8276u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateIcemanHotspotsummon.prototype
        {   8280u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Traits/OffenseTrait.prototype
        {   8281u, "She-Hulk" },  // Powers/Player/SheHulk/ObjectionMoveToStrikeDamageBonus.prototype
        {   8283u, "Carnage" },  // Powers/Player/Carnage/ReapingTimeHealthOnHitProc.prototype
        {   8285u, "Luke Cage" },  // Powers/Player/LukeCage/Pummel6thAttack.prototype
        {   8286u, "Cyclops" },  // Powers/Player/Cyclops/DisengagingShotEnd.prototype
        {   8287u, "Colossus" },  // Powers/Player/Colossus/MovementSpinHotspotKnockback.prototype
        {   8288u, "Taskmaster" },  // Powers/Player/Taskmaster/Traits/OffensiveTrait.prototype
        {   8290u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PullTowardsPointProcEffect.prototype
        {   8291u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveRocketRaccoonSelfBuffProcEffect.prototype
        {   8296u, "Ghost Rider" },  // Powers/Player/GhostRider/FlameScytheHotspotEffect.prototype
        {   8297u, "Daredevil" },  // Powers/Player/Daredevil/RadarPingHiddenPassive.prototype
        {   8299u, "Silver Surfer" },  // Powers/Player/SilverSurfer/DisengageExtraBeamCDR.prototype
        {   8300u, "Kitty Pryde" },  // Powers/Player/KittyPryde/ExecuteCombo.prototype
        {   8301u, "Angela" },  // Powers/Player/Angela/Traits/OffensiveTrait.prototype
        {   8303u, "Blade" },  // Powers/Player/Blade/StackableBleedMissileEffect.prototype
        {   8304u, "Carnage" },  // Powers/Player/Carnage/AxeDFABuffCombo.prototype
        {   8307u, "Daredevil" },  // Powers/Player/Daredevil/Talents/BouncingStrikeAdditionalHitsTalents.prototype
        {   8308u, "Black Cat" },  // Powers/Player/BlackCat/TumbleStealth.prototype
        {   8310u, "Hulk" },  // Powers/Player/Hulk/HighlightRampageProcEffect.prototype
        {   8311u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveBlackCat.prototype
        {   8312u, "Black Bolt" },  // Powers/Player/BlackBolt/Talents/Talent3BurstRemap.prototype
        {   8317u, "Juggernaut" },  // Powers/Player/Juggernaut/PreventMomentumDecay.prototype
        {   8320u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/DiamondWhirlwindReflectBuff.prototype
        {   8322u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCardMelee.prototype
        {   8324u, "Iron Fist" },  // Powers/Player/IronFist/CraneStanceBuff.prototype
        {   8326u, "X-23" },  // Powers/Player/X23/SignatureTranceChargeCounter.prototype
        {   8331u, "Thor" },  // Powers/Player/Thor/Traits/OffensiveTrait.prototype
        {   8332u, "Winter Soldier" },  // Powers/Player/WinterSoldier/StealthRollAssassinateCritBuff.prototype
        {   8334u, "Angela" },  // Powers/Player/Angela/UltimateBuffCombo.prototype
        {   8335u, "Carnage" },  // Powers/Player/Carnage/OrganicWebbingHotspotEffect.prototype
        {   8336u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseAoEHotspotSlowEffectCooldown.prototype
        {   8338u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/PassiveSquirrelPetsHealEffect.prototype
        {   8340u, "Black Bolt" },  // Powers/Player/BlackBolt/SuddenBeamCooldownReduction.prototype
        {   8342u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/MeleeSquirrelConeHitEffectDoT.prototype
        {   8343u, "Thing" },  // Powers/Player/Thing/Rework/YancyStreetGangPieChuckEffect.prototype
        {   8344u, "Magneto" },  // Powers/Player/Magneto/UltimateSentinelSmash.prototype
        {   8347u, "Cable" },  // Powers/Player/Cable/GreymalkinBeamHotspotEffect.prototype
        {   8348u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/BasicTripleSquirrelDeathProc.prototype
        {   8349u, "Thing" },  // Powers/Player/Thing/HotheadMeleeDoTProc.prototype
        {   8352u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallBeastMissileEffect.prototype
        {   8353u, "Elektra" },  // Powers/Player/Elektra/BlowDart.prototype
        {   8357u, "Beast" },  // Powers/Player/Beast/Talents/Talent1TumbleCharge.prototype
        {   8362u, "Deadpool" },  // Powers/Player/Deadpool/AcrobaticAttackComboEffect.prototype
        {   8364u, "Storm" },  // Powers/Player/Storm/CloakOfWindKnockbackEffect.prototype
        {   8367u, "Ant-Man" },  // Powers/Player/AntMan/GiantManFootMaxHealthBuff.prototype
        {   8371u, "Elektra" },  // Powers/Player/Elektra/RemoveNinjaSummons.prototype
        {   8376u, "Beast" },  // Powers/Player/Beast/BeastDashComboSummon.prototype
        {   8379u, "Thing" },  // Powers/Player/Thing/Rework/CallHotheadDamageShieldCombo.prototype
        {   8380u, "Deadpool" },  // Powers/Player/Deadpool/Rework/Teleport.prototype
        {   8382u, "War Machine" },  // Powers/Player/WarMachine/PlasmaCannon.prototype
        {   8387u, "Loki" },  // Powers/Player/Loki/SpiritsOfTheDeadTrigger.prototype
        {   8389u, "Human Torch" },  // Powers/Player/HumanTorch/NovaCharge.prototype
        {   8391u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CosmicRiftHotspotEffectRed.prototype
        {   8392u, "Ultron" },  // Powers/Player/Ultron/BigBigBlast.prototype
        {   8394u, "Ultron" },  // Powers/Player/Ultron/SlamDamage.prototype
        {   8395u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedFireBreathHotspotsEnhancedBig.prototype
        {   8399u, "Blade" },  // Powers/Player/Blade/HemoglycerinGauntlet.prototype
        {   8400u, "Iceman" },  // Powers/Player/Iceman/IceGolemSnowstormHotspotEffect.prototype
        {   8405u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/VenomWrithingTendrilsMissileEffect.prototype
        {   8406u, "Thing" },  // Powers/Player/Thing/Rework/CrashingLeapEnd.prototype
        {   8411u, "Ultron" },  // Powers/Player/Ultron/RapidFireLeft.prototype
        {   8416u, "Ultron" },  // Powers/Player/Ultron/RepairProtocolDroneHealingSummon.prototype
        {   8418u, "Thor" },  // Powers/Player/Thor/Rework/PBAoEStorm.prototype
        {   8421u, "Ultron" },  // Powers/Player/Ultron/CleanseSelfRezInvulnCombo.prototype
        {   8424u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateCallJeanObjectExplosion.prototype
        {   8429u, "Daredevil" },  // Powers/Player/Daredevil/ShadowStrikeKeywordCombo.prototype
        {   8430u, "Nova" },  // Powers/Player/Nova/FuriousLungeBackwardsConeCombo.prototype
        {   8431u, "Nova" },  // Powers/Player/Nova/PBAoENukeDestructibleExclude.prototype
        {   8442u, "Captain America" },  // Powers/Player/CaptainAmerica/Talents/ShieldBlockDeflect100PctSpec.prototype
        {   8443u, "Moon Knight" },  // Powers/Player/MoonKnight/RapidFireMissileEffectBouncing.prototype
        {   8444u, "Ant-Man" },  // Powers/Player/AntMan/MultiStrikeHideMeshInvulnerable.prototype
        {   8445u, "Wolverine" },  // Powers/Player/Wolverine/FuryGainCritEffect.prototype
        {   8446u, "Deadpool" },  // Powers/Player/Deadpool/Rework/StrafeRecurringShot.prototype
        {   8448u, "Carnage" },  // Powers/Player/Carnage/Ultimate.prototype
        {   8449u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent1MeleeBuff.prototype
        {   8450u, "Iron Man" },  // Powers/Player/IronMan/RepulsorBurst.prototype
        {   8452u, "Punisher" },  // Powers/Player/Punisher/Rework/PassiveToughCleanseCowerCombo.prototype
        {   8453u, "Magneto" },  // Powers/Player/Magneto/DebrisVisualPhase2.prototype
        {   8456u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedFireBreathHotspotEffect.prototype
        {   8457u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondArmorCondition.prototype
        {   8460u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Talents/Talent2SwordRemapping.prototype
        {   8461u, "Taskmaster" },  // Powers/Player/Taskmaster/ComboPointStackBuff.prototype
        {   8464u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/LightAndDarkness.prototype
        {   8465u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondHeartDiamondCombo.prototype
        {   8466u, "Human Torch" },  // Powers/Player/HumanTorch/ConsumeForEnduranceHitFX.prototype
        {   8471u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/CrescentFanCooldown.prototype
        {   8476u, "Beast" },  // Powers/Player/Beast/SleepGasGadgetSlowHSEffect.prototype
        {   8479u, "Loki" },  // Powers/Player/Loki/SpiritsOfTheDeadAttack.prototype
        {   8481u, "Blade" },  // Powers/Player/Blade/ShotgunMissileEffect.prototype
        {   8485u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MODOKPsychicShockwave.prototype
        {   8486u, "Loki" },  // Powers/Player/Loki/FrostArmorVisualEffect.prototype
        {   8487u, "War Machine" },  // Powers/Player/WarMachine/Chainsaws.prototype
        {   8490u, "Gambit" },  // Powers/Player/Gambit/GrandSlamDamageBonusBuff.prototype
        {   8492u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/WidowsGraceTalent.prototype
        {   8493u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallBeastDismount.prototype
        {   8495u, "War Machine" },  // Powers/Player/WarMachine/LifeSupportRezExplosion.prototype
        {   8496u, "Hawkeye" },  // Powers/Player/Hawkeye/Katana.prototype
        {   8497u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickVolleyGasBombCombo.prototype
        {   8498u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveLadyDeathstrike.prototype
        {   8500u, "Ultron" },  // Powers/Player/Ultron/RadiationBlastEnergySpecDoT.prototype
        {   8501u, "Beast" },  // Powers/Player/Beast/Talents/Talent3ShieldGadgetRemap.prototype
        {   8503u, "Blade" },  // Powers/Player/Blade/AdvancedTechnique.prototype
        {   8504u, "Venom" },  // Powers/Player/Venom/IchorSpikeProcVersion.prototype
        {   8512u, "Gambit" },  // Powers/Player/Gambit/BatterUpDamageBonusBuff.prototype
        {   8513u, "Black Cat" },  // Powers/Player/BlackCat/Talents/TalentConeYankDamageBonus.prototype
        {   8515u, "Punisher" },  // Powers/Player/Punisher/Traits/DefaultAmmoRegenEnd.prototype
        {   8516u, "Thor" },  // Powers/Player/Thor/Rework/Charge.prototype
        {   8518u, "Ultron" },  // Powers/Player/Ultron/Ultimate.prototype
        {   8522u, "She-Hulk" },  // Powers/Player/SheHulk/FinisherCost.prototype
        {   8523u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickQuiverGenerateToFull.prototype
        {   8526u, "Elektra" },  // Powers/Player/Elektra/SilentScreamMarkForDeathCombo.prototype
        {   8529u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ColossusMetalSkinHiddenPassive.prototype
        {   8535u, "Blade" },  // Powers/Player/Blade/Talents/PulsingUVGrenadeTalent.prototype
        {   8537u, "Nova" },  // Powers/Player/Nova/Talents/Talent3MicroSupermassivePulsar.prototype
        {   8538u, "Beast" },  // Powers/Player/Beast/TumbleComboSummon.prototype
        {   8540u, "Taskmaster" },  // Powers/Player/Taskmaster/ShieldBounceBleedMissileEffect.prototype
        {   8541u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/RhinoBigChargeMobEffect.prototype
        {   8543u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ConeRapidPunchHotspotEffect.prototype
        {   8545u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MindlessOneBeam.prototype
        {   8546u, "Rogue" },  // Powers/Player/Rogue/StolenPowerLibrarySlot8.prototype
        {   8547u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/SpendersAreFreePhoenixSpiritRegen.prototype
        {   8548u, "Loki" },  // Powers/Player/Loki/SoulCrushTransfer3.prototype
        {   8551u, "Angela" },  // Powers/Player/Angela/DeathFromAboveComboEffect.prototype
        {   8552u, "Hawkeye" },  // Powers/Player/Hawkeye/ThirtyArrowSpeedLoader.prototype
        {   8555u, "Iron Fist" },  // Powers/Player/IronFist/UltimateBuffNoStanceVisual.prototype
        {   8559u, "Daredevil" },  // Powers/Player/Daredevil/BouncingStrikeEnd.prototype
        {   8561u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BigBeam.prototype
        {   8562u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/JuggernautImInvulnerableMomentumMovementGain.prototype
        {   8564u, "Colossus" },  // Powers/Player/Colossus/ShockwaveDoT.prototype
        {   8566u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveMoonKnight.prototype
        {   8567u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/ColleenWing/ColleenWingDefaultAttack3.prototype
        {   8568u, "Carnage" },  // Powers/Player/Carnage/ProtectionGain20PctSpenderAxeThrow.prototype
        {   8569u, "Deadpool" },  // Powers/Player/Deadpool/FourthWallProcEffect.prototype
        {   8572u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MalekithDarkBeamHealthCost.prototype
        {   8576u, "Hulk" },  // Powers/Player/Hulk/Rework/MeteorInvulnerableCombo.prototype
        {   8580u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/PassiveSquirrelPets.prototype
        {   8581u, "Deadpool" },  // Powers/Player/Deadpool/Rework/LilDeadpoolDollTaunt.prototype
        {   8583u, "Green Goblin" },  // Powers/Player/GreenGoblin/FlyingFlamethrower.prototype
        {   8584u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MilesMoralesInvisSteroid.prototype
        {   8585u, "Iron Man" },  // Powers/Player/IronMan/SystemRebootHealing.prototype
        {   8587u, "Hulk" },  // Powers/Player/Hulk/Traits/MechanicTraitAngerBuffEffectIncrease.prototype
        {   8588u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BigBeamMissileEffectTooltip.prototype
        {   8589u, "She-Hulk" },  // Powers/Player/SheHulk/BarristerBeatdown5thHit.prototype
        {   8590u, "Cable" },  // Powers/Player/Cable/PsimitarCyclone.prototype
        {   8591u, "Blade" },  // Powers/Player/Blade/HelichopterHotspotEffect.prototype
        {   8592u, "Venom" },  // Powers/Player/Venom/MawFromAboveEnd.prototype
        {   8593u, "War Machine" },  // Powers/Player/WarMachine/SidekickTempInvulnCombo.prototype
        {   8594u, "Doctor Strange" },  // Powers/Player/DoctorStrange/BasicDaggersAddCharge.prototype
        {   8597u, "Psylocke" },  // Powers/Player/Psylocke/PsiKnifePsiEnergyGain.prototype
        {   8598u, "Black Panther" },  // Powers/Player/BlackPanther/FreezingDaggersSplashDamage.prototype
        {   8600u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Traits/MechanicTraitChaosEnergy.prototype
        {   8601u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HumanTorchNovaBurstOverheatEffect.prototype
        {   8603u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/HealthDefenseSelfRez.prototype
        {   8605u, "Black Bolt" },  // Powers/Player/BlackBolt/EnergyGainsCombo5Pct.prototype
        {   8609u, "Nick Fury" },  // Powers/Player/NickFury/FireteamSteroid.prototype
        {   8610u, "Hawkeye" },  // Powers/Player/Hawkeye/ShriekingArrow.prototype
        {   8612u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveDominoProcEffectDodge.prototype
        {   8617u, "Carnage" },  // Powers/Player/Carnage/Impale.prototype
        {   8618u, "Magneto" },  // Powers/Player/Magneto/ForceField.prototype
        {   8619u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/ClawSpecBleedProc.prototype
        {   8623u, "Blade" },  // Powers/Player/Blade/JustStayDownUVGrenadeDamage.prototype
        {   8625u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent5LawyerUpAsPassive.prototype
        {   8626u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/WolverineBasicRoninHealthonHit.prototype
        {   8628u, "Loki" },  // Powers/Player/Loki/DecoyIllusionStealth.prototype
        {   8629u, "Dr. Doom" },  // Powers/Player/DrDoom/BallLightning.prototype
        {   8630u, "Thor" },  // Powers/Player/Thor/Rework/ShockwaveMissileEffect.prototype
        {   8631u, "Green Goblin" },  // Powers/Player/GreenGoblin/GoblinBlast5Count.prototype
        {   8632u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ElektraShadowStrike.prototype
        {   8635u, "Thing" },  // Powers/Player/Thing/Traits/ClobberinTimeGainOnHitCallStretch.prototype
        {   8639u, "Deadpool" },  // Powers/Player/Deadpool/PowerUpSpirit.prototype
        {   8640u, "Deadpool" },  // Powers/Player/Deadpool/StrafeDeflectDodgeConditionCombo.prototype
        {   8642u, "Black Panther" },  // Powers/Player/BlackPanther/PantherGodsGraceHiddenPassive.prototype
        {   8643u, "Deadpool" },  // Powers/Player/Deadpool/Rework/PowerUpHulkDollEffect.prototype
        {   8644u, "Magik" },  // Powers/Player/Magik/NastirhBasicBlast.prototype
        {   8648u, "Psylocke" },  // Powers/Player/Psylocke/KatanaLeapSlashAoEEndMental.prototype
        {   8649u, "Loki" },  // Powers/Player/Loki/IncinerateRingDamageCombo.prototype
        {   8650u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/UnmakeRealityBossDoT.prototype
        {   8651u, "Wolverine" },  // Powers/Player/Wolverine/TornadoClawBuff.prototype
        {   8657u, "Moon Knight" },  // Powers/Player/MoonKnight/StaffPBAoEDoT.prototype
        {   8663u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/IronFistTechniqueRouter.prototype
        {   8665u, "Moon Knight" },  // Powers/Player/MoonKnight/SummonKhonshuStatueBuffPassive.prototype
        {   8667u, "X-23" },  // Powers/Player/X23/CoupDeGraceDamageCombo.prototype
        {   8669u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedAutoAttackMissile.prototype
        {   8671u, "Taskmaster" },  // Powers/Player/Taskmaster/DisengagingShotDiveKickBuff.prototype
        {   8672u, "Storm" },  // Powers/Player/Storm/Talents/HealingRain.prototype
        {   8675u, "Storm" },  // Powers/Player/Storm/SnowstormHotspotEffect.prototype
        {   8678u, "X-23" },  // Powers/Player/X23/BladeSpinMovementIntervalHotspotEffect.prototype
        {   8680u, "Winter Soldier" },  // Powers/Player/WinterSoldier/FuriousLungeEffect.prototype
        {   8685u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadlyBarrageHotspotEffect.prototype
        {   8686u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/PBAoEKnockdownDamage.prototype
        {   8688u, "Colossus" },  // Powers/Player/Colossus/SummonMagikHealProc.prototype
        {   8693u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotInfernoDeathExplosionProc.prototype
        {   8695u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent1DoubleAngerBonuses.prototype
        {   8698u, "Elektra" },  // Powers/Player/Elektra/KillCommandMysticHeal.prototype
        {   8700u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BamfYankIrresistableStun.prototype
        {   8702u, "Ant-Man" },  // Powers/Player/AntMan/TankerThrowHotspotSummon.prototype
        {   8703u, "Psylocke" },  // Powers/Player/Psylocke/CrossbowMissileEffect.prototype
        {   8710u, "Black Widow" },  // Powers/Player/BlackWidow/KnifeBleedCombo.prototype
        {   8714u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedChargeCollide.prototype
        {   8717u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotBlockadeCallIn.prototype
        {   8719u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ShieldedFistMissileEffect.prototype
        {   8723u, "Psylocke" },  // Powers/Player/Psylocke/AoEDoTWeakenCombo.prototype
        {   8725u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BamfDiveBombPersistingStealth.prototype
        {   8726u, "Dr. Doom" },  // Powers/Player/DrDoom/UltimateHiddenPassive.prototype
        {   8727u, "Iceman" },  // Powers/Player/Iceman/Talents/ShowOffIceWallFreeHotspotBeam.prototype
        {   8729u, "Psylocke" },  // Powers/Player/Psylocke/KatanaPBAoE.prototype
        {   8730u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/WarpTurretDeath.prototype
        {   8731u, "Iron Fist" },  // Powers/Player/IronFist/PummelDodgeCombo.prototype
        {   8734u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MrFantasticConeRapidPunchHSEffec.prototype
        {   8739u, "Ultron" },  // Powers/Player/Ultron/Talents/Talent5SignatureExtraSwarm.prototype
        {   8740u, "Magneto" },  // Powers/Player/Magneto/Talents/DebrisSpenderBuff.prototype
        {   8742u, "Silver Surfer" },  // Powers/Player/SilverSurfer/DeathFromBelow.prototype
        {   8745u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MoleManSummonMoloids.prototype
        {   8746u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BlankNoPowerForRestriction.prototype
        {   8752u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/ImplosionBonus.prototype
        {   8757u, "Colossus" },  // Powers/Player/Colossus/KittyPrydeSummon/PhaseAoEHotspotSlowEffect.prototype
        {   8760u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BamfYankWeaken.prototype
        {   8766u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/CrossbonesCritRatingCombo.prototype
        {   8769u, "Hawkeye" },  // Powers/Player/Hawkeye/Talents/TrickDmgMultTalent.prototype
        {   8772u, "Captain America" },  // Powers/Player/CaptainAmerica/SerumPointGainCombo.prototype
        {   8773u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/SignatureTKHurlJeanHotspotEffect.prototype
        {   8775u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveWizardBuffProcEffect.prototype
        {   8777u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Talents/InfernalBrimstoneTalent.prototype
        {   8780u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/GambitAceOfSpadesMissileEffect.prototype
        {   8782u, "Gambit" },  // Powers/Player/Gambit/BoVaultEnd.prototype
        {   8789u, "Beast" },  // Powers/Player/Beast/PummelResetConditionCancel.prototype
        {   8792u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/PassiveSquirrelDodge.prototype
        {   8793u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/PassiveSquirrelPetsProcEffect.prototype
        {   8794u, "Carnage" },  // Powers/Player/Carnage/BasicClawsBladeStaffSecondHit2.prototype
        {   8796u, "Storm" },  // Powers/Player/Storm/ChainLightningComboEffect.prototype
        {   8797u, "Blade" },  // Powers/Player/Blade/HemoglycerinGauntlet2ndAttack.prototype
        {   8799u, "Green Goblin" },  // Powers/Player/GreenGoblin/ExplosivePumpkinHotspotSummon.prototype
        {   8800u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/KronanArcanistSummonHotspot.prototype
        {   8802u, "Gambit" },  // Powers/Player/Gambit/BoVaultMeleeBuffCombo.prototype
        {   8803u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerLadyDeadpoolMeleeAttack2.prototype
        {   8804u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Talents/Talent2StealthCDRDmgBuff.prototype
        {   8805u, "Ant-Man" },  // Powers/Player/AntMan/ShrinkingStrikeConditionPower.prototype
        {   8806u, "Doctor Strange" },  // Powers/Player/DoctorStrange/MysticEnergyGainProcEffect.prototype
        {   8808u, "Juggernaut" },  // Powers/Player/Juggernaut/EarthquakeLeapMomentumGain.prototype
        {   8809u, "Storm" },  // Powers/Player/Storm/StormSurgeTornadoCombo.prototype
        {   8812u, "Cyclops" },  // Powers/Player/Cyclops/CallAngelMovementComboSummon.prototype
        {   8813u, "Black Widow" },  // Powers/Player/BlackWidow/UltimateDamageSummon.prototype
        {   8814u, "Magik" },  // Powers/Player/Magik/BoneSpiritHighlight.prototype
        {   8815u, "Vision" },  // Powers/Player/Vision/SolarFistsProcEffect.prototype
        {   8816u, "Green Goblin" },  // Powers/Player/GreenGoblin/HallucinogenicPumpkinDoTVuln.prototype
        {   8817u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MinigunStackingBuff.prototype
        {   8820u, "Venom" },  // Powers/Player/Venom/WebSplatHotspotEffect.prototype
        {   8822u, "Nova" },  // Powers/Player/Nova/PulsarExplosionRandomLocation.prototype
        {   8823u, "Ultron" },  // Powers/Player/Ultron/Talents/Talent2RangeRadiationBlast.prototype
        {   8824u, "Beast" },  // Powers/Player/Beast/MomentumDecay.prototype
        {   8830u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/DoubleStrikeBonus.prototype
        {   8833u, "Winter Soldier" },  // Powers/Player/WinterSoldier/BulletSprayHotspotEffect.prototype
        {   8834u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/AOESpamBuff.prototype
        {   8835u, "Kitty Pryde" },  // Powers/Player/KittyPryde/SignatureLockheedAoESizeBuff.prototype
        {   8836u, "Loki" },  // Powers/Player/Loki/EnchantmentDarkness.prototype
        {   8838u, "Silver Surfer" },  // Powers/Player/SilverSurfer/PowerCosmicHiddenPassive.prototype
        {   8843u, "Punisher" },  // Powers/Player/Punisher/Rework/PassiveToughCleanseCowerSelfRez.prototype
        {   8845u, "Cable" },  // Powers/Player/Cable/ParticleAcceleratorSecondaryDoT.prototype
        {   8847u, "Human Torch" },  // Powers/Player/HumanTorch/BasicFireballEffectLarger.prototype
        {   8848u, "Loki" },  // Powers/Player/Loki/ColdFrontAbsorptionShield.prototype
        {   8854u, "Moon Knight" },  // Powers/Player/MoonKnight/TumbleEnd.prototype
        {   8856u, "She-Hulk" },  // Powers/Player/SheHulk/BarExamBarThrow.prototype
        {   8857u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent5ArmorRegeneratesAfterSig.prototype
        {   8864u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent3BarExamToMissile.prototype
        {   8867u, "Nova" },  // Powers/Player/Nova/PassiveSRShieldProcEffect.prototype
        {   8868u, "Iceman" },  // Powers/Player/Iceman/FrostWedgeComboSummon.prototype
        {   8869u, "Storm" },  // Powers/Player/Storm/Talents/LightningCharged.prototype
        {   8871u, "Beast" },  // Powers/Player/Beast/Talents/Talent5BeastModeBuff.prototype
        {   8872u, "War Machine" },  // Powers/Player/WarMachine/Autogun.prototype
        {   8875u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent5AoEDoTBuffProcFilterPower.prototype
        {   8880u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DarkPhoenixJeanExplosion.prototype
        {   8882u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Traits/DefenseTrait.prototype
        {   8885u, "Iceman" },  // Powers/Player/Iceman/CleanseHiddenPassive.prototype
        {   8887u, "Gambit" },  // Powers/Player/Gambit/UltimateRogueDefaultAttackCombo3.prototype
        {   8888u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DarkPhoenixJeanBuffComboNoVisual.prototype
        {   8890u, "Deadpool" },  // Powers/Player/Deadpool/Rework/CaltropsRework.prototype
        {   8891u, "War Machine" },  // Powers/Player/WarMachine/SidekickHotspotCombo.prototype
        {   8896u, "Taskmaster" },  // Powers/Player/Taskmaster/VolleyMissileEffect.prototype
        {   8901u, "Psylocke" },  // Powers/Player/Psylocke/Traits/BarrierRegenPauseInCombat.prototype
        {   8905u, "Black Cat" },  // Powers/Player/BlackCat/ConeYank.prototype
        {   8909u, "Ghost Rider" },  // Powers/Player/GhostRider/ChainFlechetteBuffCombo.prototype
        {   8911u, "Carnage" },  // Powers/Player/Carnage/AxeDFABuffComboOnKillProc.prototype
        {   8913u, "Luke Cage" },  // Powers/Player/LukeCage/TumbleKickTalentStack.prototype
        {   8915u, "Iron Man" },  // Powers/Player/IronMan/OverheatAbove50PctBuffProc.prototype
        {   8916u, "Loki" },  // Powers/Player/Loki/UltimateIceOrb.prototype
        {   8917u, "Green Goblin" },  // Powers/Player/GreenGoblin/RocketsMissileNegatorSummon.prototype
        {   8918u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveCarnageHealProc.prototype
        {   8921u, "Ultron" },  // Powers/Player/Ultron/PhysicalDamageDefenseCondition.prototype
        {   8923u, "Carnage" },  // Powers/Player/Carnage/Talents/CarnageRules.prototype
        {   8925u, "Emma Frost" },  // Powers/Player/EmmaFrost/UltimateSpiderBlastMissileEffect.prototype
        {   8927u, "Thing" },  // Powers/Player/Thing/CallSuzieConditionDisable.prototype
        {   8928u, "Gambit" },  // Powers/Player/Gambit/Traits/KineticEnergyRegenTrigger.prototype
        {   8929u, "Iron Man" },  // Powers/Player/IronMan/SystemRebootInvulnerableEffect.prototype
        {   8930u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/PhoenixForceGeneration.prototype
        {   8931u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexSphereSRChangeProcEffect.prototype
        {   8932u, "War Machine" },  // Powers/Player/WarMachine/ThermalShotVulnerabilityCombo.prototype
        {   8936u, "Punisher" },  // Powers/Player/Punisher/Rework/ChemicalBomb.prototype
        {   8938u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/TippyToeSummonProc.prototype
        {   8940u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseTeleportSpiritRegenBonus.prototype
        {   8943u, "Iron Man" },  // Powers/Player/IronMan/SpeedRushHasteEffect.prototype
        {   8946u, "Deadpool" },  // Powers/Player/Deadpool/Rework/Bazooka.prototype
        {   8961u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/HeadbuttBarroomBrawlingBuff.prototype
        {   8963u, "Cable" },  // Powers/Player/Cable/ViperBeam.prototype
        {   8964u, "Hawkeye" },  // Powers/Player/Hawkeye/NullifierArrow.prototype
        {   8965u, "Venom" },  // Powers/Player/Venom/SwingingAssaultIchorGain.prototype
        {   8966u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/DefenseDeflectTalent.prototype
        {   8967u, "Blade" },  // Powers/Player/Blade/HandCannonMissileEffectBonusCrit.prototype
        {   8968u, "Silver Surfer" },  // Powers/Player/SilverSurfer/TimeWarpBuffCombo.prototype
        {   8969u, "Winter Soldier" },  // Powers/Player/WinterSoldier/BrawlerStanceTree3DamageBuff.prototype
        {   8971u, "Black Cat" },  // Powers/Player/BlackCat/TrapSignatureDFAHit.prototype
        {   8974u, "Thor" },  // Powers/Player/Thor/ChargeEffect.prototype
        {   8976u, "Luke Cage" },  // Powers/Player/LukeCage/ThrowCarFinisher.prototype
        {   8978u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/zzzDeprecated/IronFistChiBurstWeakenEffect.prototype
        {   8982u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HowardTheDuckDeathPunch.prototype
        {   8985u, "Emma Frost" },  // Powers/Player/EmmaFrost/UltimateBeastBite.prototype
        {   8989u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ElektraShadowStrikeMovement.prototype
        {   8991u, "Taskmaster" },  // Powers/Player/Taskmaster/UltimateCaptainAmericaFinestHour.prototype
        {   8996u, "Nick Fury" },  // Powers/Player/NickFury/MinigunAgentAttack.prototype
        {   8998u, "Black Panther" },  // Powers/Player/BlackPanther/SnareRanged.prototype
        {   9001u, "Moon Knight" },  // Powers/Player/MoonKnight/RapidFire.prototype
        {   9004u, "War Machine" },  // Powers/Player/WarMachine/Talents/Talent2Chainsaws.prototype
        {   9006u, "Iron Fist" },  // Powers/Player/IronFist/ShaolinStrikeVulnerability.prototype
        {   9007u, "Loki" },  // Powers/Player/Loki/LokiIllusionMeleeAttack2.prototype
        {   9008u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosBlastChaosVersion.prototype
        {   9009u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/CyclopsBouncingBeamChainEffect.prototype
        {   9010u, "Iceman" },  // Powers/Player/Iceman/IceArmor25PctGainCombo.prototype
        {   9011u, "X-23" },  // Powers/Player/X23/InCombatWrathRegen.prototype
        {   9014u, "Gambit" },  // Powers/Player/Gambit/CheatDeathInvulnerability.prototype
        {   9016u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ChanneledBeamPhase3Loop.prototype
        {   9017u, "Captain America" },  // Powers/Player/CaptainAmerica/BrutalStrikeDamageCombo.prototype
        {   9023u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent1RangedBuff.prototype
        {   9027u, "Black Panther" },  // Powers/Player/BlackPanther/EnervationDaggers.prototype
        {   9028u, "Ghost Rider" },  // Powers/Player/GhostRider/DeathFromAboveFireballDoTStack.prototype
        {   9029u, "Thing" },  // Powers/Player/Thing/Rework/GuessWhatTimeItIsCombo.prototype
        {   9033u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ForcePillarSlowKeywordCombo.prototype
        {   9036u, "Angela" },  // Powers/Player/Angela/Talents/HybridTreeModTalent.prototype
        {   9037u, "Dr. Doom" },  // Powers/Player/DrDoom/Talents/Talent5Hybrid.prototype
        {   9040u, "Wolverine" },  // Powers/Player/Wolverine/CantKeepMeDownDisabler.prototype
        {   9043u, "Taskmaster" },  // Powers/Player/Taskmaster/Traits/DefensiveTrait.prototype
        {   9045u, "Taskmaster" },  // Powers/Player/Taskmaster/DiveKickDisengagingShotBuff.prototype
        {   9046u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/CestusUppercutTribute.prototype
        {   9049u, "Elektra" },  // Powers/Player/Elektra/HideMeshCombo.prototype
        {   9052u, "Black Widow" },  // Powers/Player/BlackWidow/AcrobaticAttack.prototype
        {   9053u, "Psylocke" },  // Powers/Player/Psylocke/KatanaDoubleStrike.prototype
        {   9054u, "Carnage" },  // Powers/Player/Carnage/MegaClawDamage.prototype
        {   9066u, "Silver Surfer" },  // Powers/Player/SilverSurfer/TimeWarpHiddenPassive.prototype
        {   9069u, "Wolverine" },  // Powers/Player/Wolverine/Talents/Talent5AutoWetwork.prototype
        {   9072u, "She-Hulk" },  // Powers/Player/SheHulk/LawyerUpBuffCombo.prototype
        {   9084u, "Cable" },  // Powers/Player/Cable/PulseBoltMissileEffect.prototype
        {   9089u, "Psylocke" },  // Powers/Player/Psylocke/DashStealthBreakStealthOnHit.prototype
        {   9092u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SurturSwordAttackMissileEffect.prototype
        {   9093u, "Punisher" },  // Powers/Player/Punisher/Rework/BazookaHotspotEffect.prototype
        {   9094u, "Iron Fist" },  // Powers/Player/IronFist/FiveStanceMasteryStackRemovalProc.prototype
        {   9095u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SilverSurferChanneledBeamPhase2Start.prototype
        {   9096u, "Psylocke" },  // Powers/Player/Psylocke/DashStealthStealthCombo.prototype
        {   9098u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelPetDefaultAttack.prototype
        {   9105u, "Cyclops" },  // Powers/Player/Cyclops/Rework/BasicRicochet.prototype
        {   9106u, "Beast" },  // Powers/Player/Beast/BeastBamfAreaAoEHit.prototype
        {   9108u, "Vision" },  // Powers/Player/Vision/SolarChanneledEnergyBeamEffect.prototype
        {   9109u, "Gambit" },  // Powers/Player/Gambit/HighlightSurgePowers.prototype
        {   9112u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedToggleFlyOut.prototype
        {   9113u, "Green Goblin" },  // Powers/Player/GreenGoblin/SignatureResistanceCombo.prototype
        {   9115u, "Kitty Pryde" },  // Powers/Player/KittyPryde/SignatureAoEHitNewNew.prototype
        {   9116u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/IronFistTechniqueLeopardStanceBuff.prototype
        {   9118u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/ColleenWing/BladeSpin.prototype
        {   9121u, "Loki" },  // Powers/Player/Loki/DecoyIllusion.prototype
        {   9122u, "Venom" },  // Powers/Player/Venom/PBAoEBlobProcVersion.prototype
        {   9127u, "Gambit" },  // Powers/Player/Gambit/RoyalFlushVulnerabilityCombo.prototype
        {   9129u, "Luke Cage" },  // Powers/Player/LukeCage/SweetChristmasUltimateEffectSelf.prototype
        {   9130u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceRemoveAllCombo.prototype
        {   9133u, "Deadpool" },  // Powers/Player/Deadpool/Rework/GiantMalletCenterVuln.prototype
        {   9134u, "Captain America" },  // Powers/Player/CaptainAmerica/UltimateThorHammerDown.prototype
        {   9135u, "Ghost Rider" },  // Powers/Player/GhostRider/Talents/Talent2PhysicalBuild.prototype
        {   9137u, "Daredevil" },  // Powers/Player/Daredevil/Update/NunchuckAttackEnduranceRegen.prototype
        {   9138u, "Ghost Rider" },  // Powers/Player/GhostRider/Talents/Talent3HealthSpendingFireBreath.prototype
        {   9144u, "Thor" },  // Powers/Player/Thor/Traits/DefensiveTrait.prototype
        {   9145u, "Loki" },  // Powers/Player/Loki/ConeofColdHotspotSlow.prototype
        {   9147u, "Nick Fury" },  // Powers/Player/NickFury/Talents/Talent4HeadsDownRangedRemap.prototype
        {   9148u, "Blade" },  // Powers/Player/Blade/HemoglycerinGrenade.prototype
        {   9149u, "Colossus" },  // Powers/Player/Colossus/DeathFromAbove.prototype
        {   9151u, "Iron Fist" },  // Powers/Player/IronFist/DragonStanceBuff.prototype
        {   9153u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ElectroElementalStormBuff.prototype
        {   9156u, "War Machine" },  // Powers/Player/WarMachine/Talents/Talent5ReinforcedArmorPlating.prototype
        {   9158u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCardProcRecurringEffect.prototype
        {   9161u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/MeleePowersSundayPunchBuff.prototype
        {   9162u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent2TauntBuff.prototype
        {   9164u, "Angela" },  // Powers/Player/Angela/Talents/SignatureAllSword.prototype
        {   9165u, "Daredevil" },  // Powers/Player/Daredevil/UltimateShadowStrikeDrop.prototype
        {   9170u, "Winter Soldier" },  // Powers/Player/WinterSoldier/StealthRoll.prototype
        {   9171u, "Taskmaster" },  // Powers/Player/Taskmaster/ConeYank.prototype
        {   9179u, "Ant-Man" },  // Powers/Player/AntMan/Talents/ParticleOverchargeSteroidTalent.prototype
        {   9180u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseAoE.prototype
        {   9183u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/MagnetoMagneticSphereMissileEffect.prototype
        {   9185u, "Thor" },  // Powers/Player/Thor/Rework/StormHammerThrowMissileEffect.prototype
        {   9187u, "Elektra" },  // Powers/Player/Elektra/Talents/NinjaWarriorAlliesProc.prototype
        {   9189u, "Iceman" },  // Powers/Player/Iceman/HotspotBeam.prototype
        {   9193u, "Wolverine" },  // Powers/Player/Wolverine/BasicBleedDoT.prototype
        {   9197u, "Black Widow" },  // Powers/Player/BlackWidow/TumbleStealth.prototype
        {   9199u, "Beast" },  // Powers/Player/Beast/ShieldGadgetHotspotEffect.prototype
        {   9202u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CosmicGiftResetRotation.prototype
        {   9203u, "Deadpool" },  // Powers/Player/Deadpool/Rework/CaltropsReworkHotspotEffectMeleeSpec.prototype
        {   9207u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCardStage2.prototype
        {   9209u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MoleManMoloidLeaperLeapAttack.prototype
        {   9213u, "Beast" },  // Powers/Player/Beast/MomentumStartGaining.prototype
        {   9214u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Execute.prototype
        {   9216u, "Dr. Doom" },  // Powers/Player/DrDoom/ElectricBlastAreaEffect.prototype
        {   9217u, "Gambit" },  // Powers/Player/Gambit/FoldEmStunCombo.prototype
        {   9221u, "Punisher" },  // Powers/Player/Punisher/Rework/RpgRingDamageCombo.prototype
        {   9224u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/ChargedPBAoEConeYank.prototype
        {   9229u, "Green Goblin" },  // Powers/Player/GreenGoblin/Talents/SuperSpinTalent.prototype
        {   9235u, "Carnage" },  // Powers/Player/Carnage/ProtectionGain2Pt5PctSpender.prototype
        {   9236u, "Storm" },  // Powers/Player/Storm/SurgeGainMechanic.prototype
        {   9241u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TelepathicIllusionJeanHotspotSummonCombo.prototype
        {   9246u, "Beast" },  // Powers/Player/Beast/Traits/OffenseTrait.prototype
        {   9247u, "Deadpool" },  // Powers/Player/Deadpool/Rework/StrafeGrenades.prototype
        {   9249u, "Ultron" },  // Powers/Player/Ultron/DroneStrafeMissileEffect.prototype
        {   9251u, "Blade" },  // Powers/Player/Blade/Talents/SignatureMapTalent.prototype
        {   9253u, "Gambit" },  // Powers/Player/Gambit/Talents/Talent3ShuffleUpAndDeal.prototype
        {   9254u, "X-23" },  // Powers/Player/X23/CoupDeGrace.prototype
        {   9255u, "Thing" },  // Powers/Player/Thing/UltimateGroupBuffEffect.prototype
        {   9257u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ObfuscationCooldownDisplay.prototype
        {   9258u, "Nick Fury" },  // Powers/Player/NickFury/Talents/Talent1LifeModelDecoy.prototype
        {   9259u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/GunTurretChargeBeam.prototype
        {   9260u, "Iron Fist" },  // Powers/Player/IronFist/FlowBuff.prototype
        {   9261u, "Iron Man" },  // Powers/Player/IronMan/FreonRay.prototype
        {   9265u, "Iceman" },  // Powers/Player/Iceman/IceGolemSummonCombo.prototype
        {   9268u, "Hawkeye" },  // Powers/Player/Hawkeye/PinningShotMissileEffect.prototype
        {   9271u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ElectricAoEGadgetTeslaCoilSummon.prototype
        {   9273u, "Carnage" },  // Powers/Player/Carnage/TransfusionFullTransferCombo.prototype
        {   9275u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent1MentalBuffRevertStateOnEnd.prototype
        {   9276u, "Black Bolt" },  // Powers/Player/BlackBolt/Bolt.prototype
        {   9277u, "Angela" },  // Powers/Player/Angela/RibbonChannelHotspotIntervalEffect.prototype
        {   9278u, "Luke Cage" },  // Powers/Player/LukeCage/PunchTheGroundVulnCombo.prototype
        {   9279u, "Winter Soldier" },  // Powers/Player/WinterSoldier/TripleShotMissileEffect3.prototype
        {   9283u, "Magik" },  // Powers/Player/Magik/LifeTapComboWeakenSpec.prototype
        {   9285u, "Deadpool" },  // Powers/Player/Deadpool/Talents/SelfDestructBangBangTalent.prototype
        {   9286u, "Juggernaut" },  // Powers/Player/Juggernaut/ImInvulnerable.prototype
        {   9287u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedChargeHotspotSummon.prototype
        {   9298u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent4DamageBasedOnArmor.prototype
        {   9303u, "Magneto" },  // Powers/Player/Magneto/UltimateExplosion2MarkerSummon.prototype
        {   9309u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SurturSwordAttack.prototype
        {   9311u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HumanTorchNovaBurstOverheatSummonHotspots.prototype
        {   9312u, "Thor" },  // Powers/Player/Thor/Rework/Taunt.prototype
        {   9313u, "Jean Grey" },  // Powers/Player/JeanGrey/Traits/DefenseTrait.prototype
        {   9316u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/KronanArcanistSummonHotspotSlowEffect.prototype
        {   9322u, "Dr. Doom" },  // Powers/Player/DrDoom/Traits/DefenseTrait.prototype
        {   9323u, "Angela" },  // Powers/Player/Angela/Talents/RibbonBuffs.prototype
        {   9325u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/Microbeams.prototype
        {   9326u, "Magik" },  // Powers/Player/Magik/Talents/Talent4BloodSpirit.prototype
        {   9327u, "Cable" },  // Powers/Player/Cable/VeteranWarriorEnergyBuff.prototype
        {   9329u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SilverSurferChanneledBeamPhase2StartRefresh.prototype
        {   9333u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SurturSwordDamageRatingBuff.prototype
        {   9334u, "Venom" },  // Powers/Player/Venom/SymbioteDrainIchorHealthGain.prototype
        {   9336u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SilverSurferChanneledBeamEndCondition.prototype
        {   9337u, "Nick Fury" },  // Powers/Player/NickFury/HeadsDownRanged.prototype
        {   9338u, "Nick Fury" },  // Powers/Player/NickFury/CallRedwingAnimatedActor.prototype
        {   9339u, "Nova" },  // Powers/Player/Nova/DeathFromAbove.prototype
        {   9340u, "Angela" },  // Powers/Player/Angela/Talents/SwordLungeSlowTalent.prototype
        {   9341u, "Black Bolt" },  // Powers/Player/BlackBolt/MasterBlowAoEDamage.prototype
        {   9346u, "Deadpool" },  // Powers/Player/Deadpool/Rework/GodModeInvulnCooldownDisplay.prototype
        {   9347u, "Magneto" },  // Powers/Player/Magneto/NegativePolarityComboEffect.prototype
        {   9348u, "Taskmaster" },  // Powers/Player/Taskmaster/SwordSlice.prototype
        {   9349u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/MentalOverload.prototype
        {   9350u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ReconstructionResourceGain.prototype
        {   9351u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SilverSurferChanneledBeamExtraBe.prototype
        {   9352u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent3BetterMovement.prototype
        {   9354u, "Gambit" },  // Powers/Player/Gambit/CardPickupBuffTalent.prototype
        {   9356u, "Iceman" },  // Powers/Player/Iceman/ChillPowerHiddenPassive.prototype
        {   9357u, "Winter Soldier" },  // Powers/Player/WinterSoldier/RapidFireGate.prototype
        {   9358u, "Captain America" },  // Powers/Player/CaptainAmerica/PatrioticSpeechAsCombo.prototype
        {   9362u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveLizard.prototype
        {   9364u, "Vision" },  // Powers/Player/Vision/DenseModeProcEffect.prototype
        {   9368u, "Venom" },  // Powers/Player/Venom/Talents/IchorSpikeBuff.prototype
        {   9369u, "Daredevil" },  // Powers/Player/Daredevil/Talents/BrutalStrikeFinisherCritDamage.prototype
        {   9370u, "Luke Cage" },  // Powers/Player/LukeCage/GoodAtCombosDamageRatingBuff.prototype
        {   9372u, "Ghost Rider" },  // Powers/Player/GhostRider/RideBikeHotspotEffect.prototype
        {   9373u, "Venom" },  // Powers/Player/Venom/SigFreakoutImplosionComboBoss.prototype
        {   9375u, "Black Cat" },  // Powers/Player/BlackCat/Talents/TalentAssassinateDoesntBreakStealth.prototype
        {   9378u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveAntManProcRouter.prototype
        {   9381u, "Wolverine" },  // Powers/Player/Wolverine/PBAoEVulnerableCombo.prototype
        {   9383u, "Nova" },  // Powers/Player/Nova/UltimateNovaCorpsDamageEffect.prototype
        {   9384u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/MegaSlap.prototype
        {   9385u, "Beast" },  // Powers/Player/Beast/ElectroGadgetKillGlueGadgets.prototype
        {   9386u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DarkPhoenixExpelPhoenixForce.prototype
        {   9389u, "Punisher" },  // Powers/Player/Punisher/UltimateBuffResurrectComboEffect.prototype
        {   9399u, "X-23" },  // Powers/Player/X23/PummelExecuteDamageMult.prototype
        {   9404u, "Doctor Strange" },  // Powers/Player/DoctorStrange/SevenRingsEnduranceGainProc.prototype
        {   9406u, "Loki" },  // Powers/Player/Loki/EnchantmentIce.prototype
        {   9407u, "Magneto" },  // Powers/Player/Magneto/SpawnMetalOrbProcCooldown250MS.prototype
        {   9408u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DarkPhoenixJeanPsychicAvatarBuffCombo.prototype
        {   9410u, "Captain America" },  // Powers/Player/CaptainAmerica/UltimateRevive.prototype
        {   9412u, "Punisher" },  // Powers/Player/Punisher/Rework/FlamethrowerHotspotEffectDoT.prototype
        {   9413u, "Black Widow" },  // Powers/Player/BlackWidow/RoundhouseKickSweepKickEnduranceRemove.prototype
        {   9415u, "Magik" },  // Powers/Player/Magik/NastirhDisengageSlowCombo.prototype
        {   9416u, "She-Hulk" },  // Powers/Player/SheHulk/BarristerBeatdown2ndHit.prototype
        {   9419u, "Ant-Man" },  // Powers/Player/AntMan/TankerThrow.prototype
        {   9421u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/KirigiVanishDodgeBuff.prototype
        {   9423u, "Iron Fist" },  // Powers/Player/IronFist/FiveStanceMasteryStackRemovalCombo.prototype
        {   9424u, "Hulk" },  // Powers/Player/Hulk/Rework/RawrAngerGain.prototype
        {   9433u, "Green Goblin" },  // Powers/Player/GreenGoblin/RazorBatsMissileEffect.prototype
        {   9436u, "Luke Cage" },  // Powers/Player/LukeCage/BusinessIsGoodBuff.prototype
        {   9437u, "Daredevil" },  // Powers/Player/Daredevil/Update/ClubAttackBuffCombo.prototype
        {   9438u, "Wolverine" },  // Powers/Player/Wolverine/PBAoE.prototype
        {   9439u, "Loki" },  // Powers/Player/Loki/Traits/DefenseTrait.prototype
        {   9440u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/IronFistDragonStanceWasUsed.prototype
        {   9442u, "Colossus" },  // Powers/Player/Colossus/GroundStompVulnerableCombo.prototype
        {   9443u, "Thing" },  // Powers/Player/Thing/Rework/RockslideCharge.prototype
        {   9444u, "Taskmaster" },  // Powers/Player/Taskmaster/StudentsPMCRiotPetMeleeAttack.prototype
        {   9448u, "Cable" },  // Powers/Player/Cable/PsimitarImpaleOuterDamageCombo.prototype
        {   9455u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallIcemanHotspotDamageEffect.prototype
        {   9458u, "Nightcrawler" },  // Powers/Player/Nightcrawler/SwordThrowMissileEffect.prototype
        {   9461u, "Vision" },  // Powers/Player/Vision/EnhanceRobot.prototype
        {   9462u, "Venom" },  // Powers/Player/Venom/SwingingAssaultKnockdownEffect.prototype
        {   9463u, "Cyclops" },  // Powers/Player/Cyclops/FocusBeamBuffPhase3Refresh.prototype
        {   9466u, "Punisher" },  // Powers/Player/Punisher/Rework/ArmorPiercingInstantKillPopcorn.prototype
        {   9467u, "Nick Fury" },  // Powers/Player/NickFury/DriveByMissilePower.prototype
        {   9468u, "Iron Man" },  // Powers/Player/IronMan/SonicShockwaveDoTCombo.prototype
        {   9472u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SecondaryResourceResetMoonKnight.prototype
        {   9474u, "Black Bolt" },  // Powers/Player/BlackBolt/DeathFromAboveStart.prototype
        {   9476u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/BoomerangBubble.prototype
        {   9477u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/FlyingKick.prototype
        {   9481u, "Storm" },  // Powers/Player/Storm/ChargedStrikeHiddenPassiveProcEf.prototype
        {   9482u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/BurrowInvuln.prototype
        {   9484u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ThorGodlyValor.prototype
        {   9485u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshuSteroidSpiritVisual1.prototype
        {   9486u, "Iron Fist" },  // Powers/Player/IronFist/BlackBlackPoisonTouchHotspotSummon.prototype
        {   9487u, "Loki" },  // Powers/Player/Loki/ColdFrontFrostNovaComboDelayed.prototype
        {   9488u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/TooHotToHit.prototype
        {   9489u, "She-Hulk" },  // Powers/Player/SheHulk/CeaseAndDesistDoT.prototype
        {   9490u, "Iron Man" },  // Powers/Player/IronMan/UltimateSuitWristRocketMEffect.prototype
        {   9493u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/ForceFieldDeflectionEffect.prototype
        {   9496u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MinigunSelfAudioCombo.prototype
        {   9499u, "Ant-Man" },  // Powers/Player/AntMan/ThrowCar.prototype
        {   9501u, "Human Torch" },  // Powers/Player/HumanTorch/ConsumeIncrementPR2OnHotspotNegated.prototype
        {   9503u, "Daredevil" },  // Powers/Player/Daredevil/Update/NunchuckBulldozeHotspotEffect.prototype
        {   9504u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzyExecuteCombo.prototype
        {   9505u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Ultimate.prototype
        {   9518u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/SignatureCooldownReductionTalent.prototype
        {   9519u, "Juggernaut" },  // Powers/Player/Juggernaut/SundayPunch.prototype
        {   9522u, "Iron Man" },  // Powers/Player/IronMan/Traits/OffenseTrait.prototype
        {   9524u, "Hulk" },  // Powers/Player/Hulk/Rework/ShockwavePBAoELarge.prototype
        {   9528u, "X-23" },  // Powers/Player/X23/Talents/Talent4SigPummelExecuteCharge.prototype
        {   9529u, "Wolverine" },  // Powers/Player/Wolverine/Traits/FuryGainProcEffect.prototype
        {   9532u, "Thing" },  // Powers/Player/Thing/Traits/ClobberinTimeGainOnGotHit.prototype
        {   9533u, "Thing" },  // Powers/Player/Thing/Ultimate.prototype
        {   9537u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateHiddenPassiveSigSynergy.prototype
        {   9538u, "Taskmaster" },  // Powers/Player/Taskmaster/Roundhouse.prototype
        {   9542u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshuSteroidHealthVisual3.prototype
        {   9543u, "Moon Knight" },  // Powers/Player/MoonKnight/CrescentBolaMissileEffect.prototype
        {   9545u, "Nick Fury" },  // Powers/Player/NickFury/Traits/DefaultAmmoRegen.prototype
        {   9549u, "Iron Fist" },  // Powers/Player/IronFist/ChiSteroidSnakeFangStanceBuff.prototype
        {   9552u, "Magneto" },  // Powers/Player/Magneto/DebrisVisualHiddenPassive.prototype
        {   9554u, "Emma Frost" },  // Powers/Player/EmmaFrost/UltimateRobotDeathProcTrigger.prototype
        {   9555u, "Daredevil" },  // Powers/Player/Daredevil/Traits/OffenseTrait.prototype
        {   9557u, "Thor" },  // Powers/Player/Thor/Rework/ElectricallyCharged.prototype
        {   9561u, "Taskmaster" },  // Powers/Player/Taskmaster/BoomerangArrowMissileEffect.prototype
        {   9567u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/GunTurretBasicRifleMissileEffect.prototype
        {   9569u, "Iron Fist" },  // Powers/Player/IronFist/NinjutsuDashPBAoE.prototype
        {   9570u, "War Machine" },  // Powers/Player/WarMachine/UltimateSidekick.prototype
        {   9571u, "Loki" },  // Powers/Player/Loki/Talents/FourRealmsIncinerate.prototype
        {   9572u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/MysticismRestorationCritEffect.prototype
        {   9575u, "Magneto" },  // Powers/Player/Magneto/MagneticSphereMissileEffect.prototype
        {   9576u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveSabretoothLifeOnHitProc.prototype
        {   9577u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/SignatureCrushingLeap.prototype
        {   9581u, "Iron Fist" },  // Powers/Player/IronFist/FlyingKickPBAoE.prototype
        {   9583u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzyCooldown.prototype
        {   9585u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/KittyPrydeDeathFromBelow.prototype
        {   9588u, "Vision" },  // Powers/Player/Vision/HealingNanitesHiddenPassive.prototype
        {   9590u, "Gambit" },  // Powers/Player/Gambit/Talents/Talent1LessDowntime.prototype
        {   9591u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TelepathicIllusionJeanXmenSummon.prototype
        {   9592u, "Nova" },  // Powers/Player/Nova/FuriousLungeIncrementCharge.prototype
        {   9597u, "Nick Fury" },  // Powers/Player/NickFury/EyesEverywhereGrenade.prototype
        {   9598u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickQuiverComboExplosiveArrow.prototype
        {   9601u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/PsychicSpearChargeWhipVuln.prototype
        {   9605u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHoleHotspotEffectRed.prototype
        {   9607u, "Blade" },  // Powers/Player/Blade/StakeThrowerMissileEffect.prototype
        {   9608u, "Thing" },  // Powers/Player/Thing/Rework/KnockoutHeadbuttDamageBuffCombo.prototype
        {   9610u, "Magneto" },  // Powers/Player/Magneto/DebrisShot.prototype
        {   9611u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/AutoSlapBeam.prototype
        {   9614u, "Blade" },  // Powers/Player/Blade/StakeThroughTheHeartDamageCombo.prototype
        {   9615u, "Iron Fist" },  // Powers/Player/IronFist/DualStancePersistantBuffCombo.prototype
        {   9620u, "Colossus" },  // Powers/Player/Colossus/Shockwave.prototype
        {   9622u, "Black Panther" },  // Powers/Player/BlackPanther/UltimateSummonPanthernado.prototype
        {   9623u, "Colossus" },  // Powers/Player/Colossus/MovementSpinHotspotEffect.prototype
        {   9625u, "Black Cat" },  // Powers/Player/BlackCat/MasterThiefStunEffect.prototype
        {   9626u, "War Machine" },  // Powers/Player/WarMachine/ChainsawImpaleHit2.prototype
        {   9627u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ImplosionGadgetEffectAsCombo.prototype
        {   9630u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCard.prototype
        {   9633u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GorgonStoneGazeEffect.prototype
        {   9635u, "Gambit" },  // Powers/Player/Gambit/StacktheDeckConditionRemovalCombo.prototype
        {   9636u, "Blade" },  // Powers/Player/Blade/UVGrenadeStun.prototype
        {   9638u, "Venom" },  // Powers/Player/Venom/UltimateTriggerCondition.prototype
        {   9641u, "Dr. Doom" },  // Powers/Player/DrDoom/AutoRevive.prototype
        {   9645u, "Ultron" },  // Powers/Player/Ultron/RangeDronePulseBolt.prototype
        {   9648u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent5MeteorBonus.prototype
        {   9649u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/BasicMelee.prototype
        {   9650u, "Winter Soldier" },  // Powers/Player/WinterSoldier/TeamStealthHiddenPassive.prototype
        {   9651u, "Black Cat" },  // Powers/Player/BlackCat/Talents/TalentSignatureBleed.prototype
        {   9653u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ImplosionExplosionCondition.prototype
        {   9654u, "Venom" },  // Powers/Player/Venom/YankIchorGain.prototype
        {   9657u, "Doctor Strange" },  // Powers/Player/DoctorStrange/MysticEnergyOrbVisual3.prototype
        {   9658u, "Venom" },  // Powers/Player/Venom/ConeTendrilsHealthGain.prototype
        {   9660u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallAngelEffect1.prototype
        {   9662u, "Deadpool" },  // Powers/Player/Deadpool/Rework/BangBangMissileEffect.prototype
        {   9663u, "Iron Fist" },  // Powers/Player/IronFist/Talents/Talent4ChiPunch.prototype
        {   9666u, "Captain America" },  // Powers/Player/CaptainAmerica/DeathFromAbove.prototype
        {   9667u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/UltimateFFFireHotspotEffect.prototype
        {   9668u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/BigChargeInstagib.prototype
        {   9669u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/UltimateTransformEndExplosion.prototype
        {   9670u, "Black Bolt" },  // Powers/Player/BlackBolt/AutoReviveCooldownDisplay.prototype
        {   9672u, "Carnage" },  // Powers/Player/Carnage/ExcessTalentsIncrement3.prototype
        {   9673u, "Silver Surfer" },  // Powers/Player/SilverSurfer/PassiveOffense.prototype
        {   9674u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ChanneledBeamStackCounter.prototype
        {   9676u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Traits/OffenseTrait.prototype
        {   9679u, "Dr. Doom" },  // Powers/Player/DrDoom/Talents/Talent1MagicOrbSmart.prototype
        {   9680u, "Emma Frost" },  // Powers/Player/EmmaFrost/AmpControlledMobComboProcTaunt.prototype
        {   9684u, "Hulk" },  // Powers/Player/Hulk/Traits/AngerGainOnHit.prototype
        {   9685u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/TeamBuffer.prototype
        {   9688u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallAngelProc.prototype
        {   9693u, "Iron Fist" },  // Powers/Player/IronFist/Talents/Talent2PummelDamageMult.prototype
        {   9694u, "Carnage" },  // Powers/Player/Carnage/MacePummelDamageCombo.prototype
        {   9695u, "War Machine" },  // Powers/Player/WarMachine/UltimateSidekickBuffCombo.prototype
        {   9696u, "Punisher" },  // Powers/Player/Punisher/Talents/AutomaticShotgun.prototype
        {   9698u, "Venom" },  // Powers/Player/Venom/SymbioteSummonProc.prototype
        {   9700u, "Black Cat" },  // Powers/Player/BlackCat/TaserTrapHotspotEffect.prototype
        {   9702u, "Elektra" },  // Powers/Player/Elektra/Ultimate.prototype
        {   9703u, "Colossus" },  // Powers/Player/Colossus/MetalChargeComboEffect.prototype
        {   9704u, "Gambit" },  // Powers/Player/Gambit/Talents/Talent4AllOutOfCards.prototype
        {   9706u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GrootSummonProc.prototype
        {   9709u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/BasicTripleSquirrelEffect.prototype
        {   9712u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/KittyPrydeLockheedChargeMissileEffect.prototype
        {   9717u, "X-23" },  // Powers/Player/X23/SignatureTranceCombo.prototype
        {   9718u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallBeastMelee2.prototype
        {   9719u, "Psylocke" },  // Powers/Player/Psylocke/PsiKatanaConeMental.prototype
        {   9723u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GrimReaperEnergyBlastHeal.prototype
        {   9725u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/AutoReconstruct.prototype
        {   9726u, "Punisher" },  // Powers/Player/Punisher/Rework/BackwardsTumbleGrenadeVisualCombo.prototype
        {   9730u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/GoForTheEyesHit6.prototype
        {   9732u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ForceDashEffect.prototype
        {   9733u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallIcemanSummon.prototype
        {   9735u, "Psylocke" },  // Powers/Player/Psylocke/Implosion.prototype
        {   9736u, "Wolverine" },  // Powers/Player/Wolverine/SignatureDashSlash.prototype
        {   9737u, "Nick Fury" },  // Powers/Player/NickFury/MolecularGrenadeWeakenCombo.prototype
        {   9739u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedChargeReturnTimer.prototype
        {   9740u, "Punisher" },  // Powers/Player/Punisher/FlamethrowerHotspotEffectDamage.prototype
        {   9741u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/BasicRifle.prototype
        {   9743u, "Black Panther" },  // Powers/Player/BlackPanther/Talents/EnervateStackTalent.prototype
        {   9744u, "Ultron" },  // Powers/Player/Ultron/ConcussionBlast.prototype
        {   9746u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PsylockeLunge.prototype
        {   9747u, "Beast" },  // Powers/Player/Beast/BeastBamfAreaHiit.prototype
        {   9748u, "Nick Fury" },  // Powers/Player/NickFury/ChanneledBeamInvulnerability.prototype
        {   9755u, "Storm" },  // Powers/Player/Storm/LightningColumnHSEffect.prototype
        {   9757u, "Juggernaut" },  // Powers/Player/Juggernaut/UltimateProcEffect.prototype
        {   9758u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/JessicaJones/ThrowConcrete.prototype
        {   9761u, "Jean Grey" },  // Powers/Player/JeanGrey/PhoenixModeToggleHiddenPassive.prototype
        {   9762u, "Carnage" },  // Powers/Player/Carnage/Talents/FullTransfusion.prototype
        {   9764u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Talents/Talent1IAmGroot.prototype
        {   9765u, "Loki" },  // Powers/Player/Loki/LightBeamIllusionPower.prototype
        {   9766u, "War Machine" },  // Powers/Player/WarMachine/DeathFromAbove.prototype
        {   9769u, "Punisher" },  // Powers/Player/Punisher/Rework/ReloadHiddenPassive.prototype
        {   9770u, "Thing" },  // Powers/Player/Thing/Rework/Knockout3rdAttack.prototype
        {   9771u, "Nightcrawler" },  // Powers/Player/Nightcrawler/SwordPummel5thAttack.prototype
        {   9772u, "Blade" },  // Powers/Player/Blade/PassiveReviveCombo.prototype
        {   9773u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AmpControlledMobExplosion.prototype
        {   9774u, "Hawkeye" },  // Powers/Player/Hawkeye/TurretArrowHotspotEffect.prototype
        {   9775u, "Blade" },  // Powers/Player/Blade/HemoExplosionFearCombo.prototype
        {   9783u, "Black Cat" },  // Powers/Player/BlackCat/SignatureShrapnelTrap.prototype
        {   9785u, "Blade" },  // Powers/Player/Blade/SwordDashEffect.prototype
        {   9787u, "Hawkeye" },  // Powers/Player/Hawkeye/TaserArrowMissileEffectThreeRoundBurst.prototype
        {   9790u, "Colossus" },  // Powers/Player/Colossus/FastballSpecialThrownMissileEffect.prototype
        {   9795u, "Thing" },  // Powers/Player/Thing/Talents/Talent1YancyStreetBuff.prototype
        {   9797u, "Human Torch" },  // Powers/Player/HumanTorch/FlameWaveMissileEffectDoT.prototype
        {   9800u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/NickFuryPetSteroid.prototype
        {   9804u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MagnetoAllInImplosionCombo.prototype
        {   9807u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/OffenseAuraDamage.prototype
        {   9817u, "Iceman" },  // Powers/Player/Iceman/IceSlash.prototype
        {   9825u, "Blade" },  // Powers/Player/Blade/LongLivedHealthOnHitProcMelee.prototype
        {   9827u, "Emma Frost" },  // Powers/Player/EmmaFrost/Traits/OffenseTraitDiamondForm.prototype
        {   9828u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/KineticBoltPhoenixEffect.prototype
        {   9829u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerDeadpoolTheKidGatlingGunMissileEffect.prototype
        {   9833u, "Beast" },  // Powers/Player/Beast/Talents/Talent2FlyingBeatdownRemap.prototype
        {   9835u, "Black Widow" },  // Powers/Player/BlackWidow/FlipKickSummon.prototype
        {   9836u, "Punisher" },  // Powers/Player/Punisher/Talents/Minigun.prototype
        {   9838u, "Nova" },  // Powers/Player/Nova/ArcBurst.prototype
        {   9839u, "Iron Fist" },  // Powers/Player/IronFist/NinjutsuDash.prototype
        {   9840u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateCallAngelProc.prototype
        {   9844u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/SquirrelFromAboveProcEffect.prototype
        {   9845u, "Winter Soldier" },  // Powers/Player/WinterSoldier/DispatchedProc.prototype
        {   9846u, "X-23" },  // Powers/Player/X23/SigBladeDanceHotspotEffect.prototype
        {   9848u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/InvisibilityAutoProcEffect.prototype
        {   9854u, "Black Widow" },  // Powers/Player/BlackWidow/SweepingKick.prototype
        {   9855u, "Black Panther" },  // Powers/Player/BlackPanther/Talents/UnseenPredatorHealRangedTalent.prototype
        {   9857u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseAoEHotspotEffect.prototype
        {   9860u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/ScarletWitchHexSphereMissileEffect.prototype
        {   9861u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/UltimateBubblestormComboUnbreakable.prototype
        {   9862u, "Cyclops" },  // Powers/Player/Cyclops/OpticBlastMissileEffect.prototype
        {   9864u, "Winter Soldier" },  // Powers/Player/WinterSoldier/SniperShotCooldownCondition.prototype
        {   9868u, "Loki" },  // Powers/Player/Loki/FrostNovaConsumeFreeze.prototype
        {   9869u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveMilesMoralesProcEffect.prototype
        {   9870u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzyFinishingHitRanged.prototype
        {   9871u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ShockwavePBAOESpiritGain.prototype
        {   9872u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HydeDirectedShockwaveMissileBoosted.prototype
        {   9875u, "Vision" },  // Powers/Player/Vision/SolarEnergyHiddenPassive.prototype
        {   9877u, "Iceman" },  // Powers/Player/Iceman/IceGolemHiddenPassiveToggler.prototype
        {   9879u, "Angela" },  // Powers/Player/Angela/DoubleAxeThrow.prototype
        {   9883u, "Cable" },  // Powers/Player/Cable/VortexGrenadeImplosion.prototype
        {   9884u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/PBAoEKnockdownBleedCombo.prototype
        {   9887u, "Black Widow" },  // Powers/Player/BlackWidow/InvisibleTauntPower.prototype
        {   9889u, "Daredevil" },  // Powers/Player/Daredevil/HighlightComboFinishers.prototype
        {   9890u, "Captain America" },  // Powers/Player/CaptainAmerica/BoomerangThrowMissileEffectSerum.prototype
        {   9891u, "Black Bolt" },  // Powers/Player/BlackBolt/SteroidAoEDamageCombo.prototype
        {   9892u, "Deadpool" },  // Powers/Player/Deadpool/Rework/BangBangExplosive.prototype
        {   9894u, "Black Panther" },  // Powers/Player/BlackPanther/EnervationDaggersDoTStackSingle.prototype
        {   9896u, "Magneto" },  // Powers/Player/Magneto/SpawnMetalOrbCombo10Orbs.prototype
        {   9897u, "Doctor Strange" },  // Powers/Player/DoctorStrange/DemonsOfDenakTransfer5.prototype
        {   9898u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/ImplosionJean.prototype
        {   9901u, "Daredevil" },  // Powers/Player/Daredevil/Talents/ComboHealTalent.prototype
        {   9903u, "Rogue" },  // Powers/Player/Rogue/RecallOverloadMental.prototype
        {   9904u, "Juggernaut" },  // Powers/Player/Juggernaut/PeoplesElbow.prototype
        {   9905u, "Daredevil" },  // Powers/Player/Daredevil/Talents/SigDoubleDamageCenterTalent.prototype
        {   9909u, "Doctor Strange" },  // Powers/Player/DoctorStrange/DemonsOfDenakTransfer.prototype
        {   9910u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/UltimateFFSpinCombo.prototype
        {   9912u, "Deadpool" },  // Powers/Player/Deadpool/Rework/SelfDestructBurnCombo.prototype
        {   9913u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/FlameCyclone.prototype
        {   9915u, "Punisher" },  // Powers/Player/Punisher/Rework/BulletSprayHotspotEffect.prototype
        {   9916u, "Black Cat" },  // Powers/Player/BlackCat/BasicClaws.prototype
        {   9920u, "Ultron" },  // Powers/Player/Ultron/ConcussionBlastComboEffect.prototype
        {   9922u, "Ant-Man" },  // Powers/Player/AntMan/PymSuitShrinkConditionPower.prototype
        {   9923u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BigLimboDemonShockwave.prototype
        {   9925u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaoticDebuffSelfHeal.prototype
        {   9926u, "Angela" },  // Powers/Player/Angela/ExecuteChop.prototype
        {   9928u, "Winter Soldier" },  // Powers/Player/WinterSoldier/SpinningMinesEnduranceRestoreCombo.prototype
        {   9929u, "Thing" },  // Powers/Player/Thing/YancyStreetGangConditionDisable.prototype
        {   9930u, "Nova" },  // Powers/Player/Nova/PBAoENukeAsProc.prototype
        {   9932u, "Black Bolt" },  // Powers/Player/BlackBolt/ChanneledBeamEffect.prototype
        {   9933u, "Dr. Doom" },  // Powers/Player/DrDoom/Talents/Talent3DoomBurstBonus.prototype
        {   9934u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateBattleVanSummon.prototype
        {   9936u, "Nightcrawler" },  // Powers/Player/Nightcrawler/TeleportBackstab.prototype
        {   9937u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickQuiverComboFreezeArrow.prototype
        {   9942u, "Black Panther" },  // Powers/Player/BlackPanther/SmokeScreenHiddenPassive.prototype
        {   9944u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallMagnetoHotspotDamageEffect.prototype
        {   9945u, "Taskmaster" },  // Powers/Player/Taskmaster/UltimateDaredevilShadowStrike.prototype
        {   9947u, "Wolverine" },  // Powers/Player/Wolverine/Traits/FuryOnBleedHit.prototype
        {   9948u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/WindAndLightning.prototype
        {   9957u, "X-23" },  // Powers/Player/X23/BladeSpin.prototype
        {   9958u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Traits/ChaosRegenProcEffect.prototype
        {   9960u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/BasicRangedSquirrelMissileEffect.prototype
        {   9962u, "Black Cat" },  // Powers/Player/BlackCat/MasterThief.prototype
        {   9964u, "Beast" },  // Powers/Player/Beast/TetherballPBAoE.prototype
        {   9966u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/FrostGiantFrostNova.prototype
        {   9971u, "Ghost Rider" },  // Powers/Player/GhostRider/ChargeUpBikeHotspotEffect.prototype
        {   9972u, "Ant-Man" },  // Powers/Player/AntMan/AntSpenderSpiritRestore20pct.prototype
        {   9973u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Traits/ChaosEnergyGain.prototype
        {   9974u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelBombsOnDeathDropAcorn.prototype
        {   9975u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotFlyerSkillshot2.prototype
        {   9977u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/BFGHotspotSlowTalented.prototype
        {   9978u, "Blade" },  // Powers/Player/Blade/SpecRotationalBloodlustMaxedSerumTimer.prototype
        {   9979u, "Vision" },  // Powers/Player/Vision/SolarEnergyChargingBuff.prototype
        {   9980u, "Daredevil" },  // Powers/Player/Daredevil/TumbleKnockdownEffect.prototype
        {   9983u, "Carnage" },  // Powers/Player/Carnage/Traits/CarnageBloodlustEffectRemove.prototype
        {   9985u, "Black Bolt" },  // Powers/Player/BlackBolt/Steroid.prototype
        {   9990u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/PassiveSquirrelPetsHiddenPassive.prototype
        {   9993u, "Carnage" },  // Powers/Player/Carnage/ProtectionGain20PctSpender.prototype
        {   9996u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LukeCagePummel4thAttack.prototype
        {   9997u, "Ant-Man" },  // Powers/Player/AntMan/AntVulnerabilityProc.prototype
        {   9998u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/NeuralNetworkTransfer3rdWave.prototype
        {  10004u, "Iron Man" },  // Powers/Player/IronMan/Traits/OffenseTraitStatConversionEffect.prototype
        {  10005u, "Blade" },  // Powers/Player/Blade/UVGrenade.prototype
        {  10008u, "Luke Cage" },  // Powers/Player/LukeCage/DefensiveLeader.prototype
        {  10009u, "Elektra" },  // Powers/Player/Elektra/KnifeThrow.prototype
        {  10010u, "Venom" },  // Powers/Player/Venom/BigPunchHealthGain.prototype
        {  10011u, "Elektra" },  // Powers/Player/Elektra/SpinningStrikeBleed.prototype
        {  10013u, "Black Bolt" },  // Powers/Player/BlackBolt/MasterBlowAoECombo.prototype
        {  10015u, "Black Panther" },  // Powers/Player/BlackPanther/DoraSummonCombo.prototype
        {  10016u, "Iron Man" },  // Powers/Player/IronMan/Microlasers.prototype
        {  10018u, "Thing" },  // Powers/Player/Thing/CallHotheadConditionDisable.prototype
        {  10020u, "Hulk" },  // Powers/Player/Hulk/Rework/WorldbreakerHotspotEffect.prototype
        {  10021u, "Magik" },  // Powers/Player/Magik/SoulCaptureMinionBuff.prototype
        {  10024u, "Venom" },  // Powers/Player/Venom/SigFreakoutDamageNegationHealthLower.prototype
        {  10025u, "Thor" },  // Powers/Player/Thor/Rework/ImmortalCombatRestore.prototype
        {  10030u, "Deadpool" },  // Powers/Player/Deadpool/Rework/StabbyFlipMoveSpeedBuff.prototype
        {  10031u, "Moon Knight" },  // Powers/Player/MoonKnight/DeathFromAboveEnd.prototype
        {  10036u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/DoubleStrikeDamageSecondHitDamageCombo.prototype
        {  10037u, "Magik" },  // Powers/Player/Magik/SoulCaptureHealingCombo.prototype
        {  10040u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/NightcrawlerValiantLeapEnd.prototype
        {  10041u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateBattleVanGunHotspotEff.prototype
        {  10043u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzyFinishingExecuteRanged.prototype
        {  10044u, "Psylocke" },  // Powers/Player/Psylocke/LungeEffect.prototype
        {  10046u, "Thing" },  // Powers/Player/Thing/Rework/LampBatThrowMissileEffect.prototype
        {  10047u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseDashEffect.prototype
        {  10049u, "Loki" },  // Powers/Player/Loki/ConeOfMagic.prototype
        {  10053u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshuSteroidHealth.prototype
        {  10056u, "Cable" },  // Powers/Player/Cable/Talents/TKSpearSlamBuff.prototype
        {  10058u, "War Machine" },  // Powers/Player/WarMachine/UltimateHiddenPassive.prototype
        {  10062u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/UnstoppableChargeCDR.prototype
        {  10065u, "Black Cat" },  // Powers/Player/BlackCat/GasTrapExplosion.prototype
        {  10066u, "Wolverine" },  // Powers/Player/Wolverine/FrenzyStackingBuff.prototype
        {  10069u, "Elektra" },  // Powers/Player/Elektra/Talents/MarkSpreadingNoCharges.prototype
        {  10071u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HydeDirectedShockwaveMissileCombo.prototype
        {  10072u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveGamora.prototype
        {  10074u, "Nick Fury" },  // Powers/Player/NickFury/BulletSprayHotspotEffect.prototype
        {  10075u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Talents/Talent5SignatureMoreHits.prototype
        {  10076u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Ultimate.prototype
        {  10078u, "Taskmaster" },  // Powers/Player/Taskmaster/PoisonGasBombHotspotEffect.prototype
        {  10079u, "Ghost Rider" },  // Powers/Player/GhostRider/BasicFireball.prototype
        {  10080u, "Luke Cage" },  // Powers/Player/LukeCage/PummelNoFinisher7thAttack.prototype
        {  10081u, "Taskmaster" },  // Powers/Player/Taskmaster/SwordSlice2ndHit.prototype
        {  10083u, "War Machine" },  // Powers/Player/WarMachine/Overheat.prototype
        {  10084u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveKraven.prototype
        {  10085u, "Magik" },  // Powers/Player/Magik/BoneSpiritHighlightAssassinate.prototype
        {  10087u, "Loki" },  // Powers/Player/Loki/Talents/FourRealmsLightBeam.prototype
        {  10088u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/ChargeUpBlowupBonus.prototype
        {  10089u, "Venom" },  // Powers/Player/Venom/SymbioteDrainPower2.prototype
        {  10090u, "Magik" },  // Powers/Player/Magik/LifeTapAmplifyDamage.prototype
        {  10092u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent5AoEDoTBuffProcHeal.prototype
        {  10095u, "Angela" },  // Powers/Player/Angela/DFACooldownStart.prototype
        {  10096u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HowardTheDuckDeathPunchExplosion.prototype
        {  10099u, "Dr. Doom" },  // Powers/Player/DrDoom/MagicOrbHotspotSummon.prototype
        {  10101u, "Captain America" },  // Powers/Player/CaptainAmerica/Talents/VibraniumBashSerumSpec.prototype
        {  10102u, "Iron Fist" },  // Powers/Player/IronFist/ChiHarmonyRapidHealingHotspotEffect.prototype
        {  10111u, "Cyclops" },  // Powers/Player/Cyclops/Rework/Roundhouse.prototype
        {  10113u, "Moon Knight" },  // Powers/Player/MoonKnight/LowHealthDamageMultProcEffect.prototype
        {  10115u, "She-Hulk" },  // Powers/Player/SheHulk/OpeningStatement.prototype
        {  10117u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/StarlordChargedEGunEarthMissileEffect.prototype
        {  10120u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/RollingGrenadesBonus.prototype
        {  10121u, "Gambit" },  // Powers/Player/Gambit/BoBeatdownBuff.prototype
        {  10122u, "Magik" },  // Powers/Player/Magik/NastirhDisengage.prototype
        {  10127u, "Loki" },  // Powers/Player/Loki/SoulCrushTransfer5.prototype
        {  10128u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateJeanSig.prototype
        {  10130u, "Thor" },  // Powers/Player/Thor/Rework/ThunderSpotAreaSecResEffect.prototype
        {  10131u, "Cable" },  // Powers/Player/Cable/VeteranWarriorHaltRecoveryCombo.prototype
        {  10134u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/ChanneledBeamStack.prototype
        {  10135u, "Rogue" },  // Powers/Player/Rogue/Traits/DefenseTrait.prototype
        {  10137u, "X-23" },  // Powers/Player/X23/BladeSpinMovementComboSummon.prototype
        {  10138u, "Doctor Strange" },  // Powers/Player/DoctorStrange/PassiveDefenseDodgeProcEffect.prototype
        {  10139u, "Juggernaut" },  // Powers/Player/Juggernaut/EarthquakeLeapHotspotEffect.prototype
        {  10140u, "Hawkeye" },  // Powers/Player/Hawkeye/FreezeArrowMissileEffectThreeRoundBurst.prototype
        {  10143u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveRedSkull.prototype
        {  10144u, "Venom" },  // Powers/Player/Venom/BigWebShootMissileEffect.prototype
        {  10147u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/GhostRiderChargeUpBikeMissileEffect.prototype
        {  10148u, "Loki" },  // Powers/Player/Loki/UltimateGrowthCondition.prototype
        {  10149u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/EmmaFrostControlMobHiddenPassive.prototype
        {  10151u, "Vision" },  // Powers/Player/Vision/EnhanceRobotAlwaysOnRemover.prototype
        {  10156u, "Green Goblin" },  // Powers/Player/GreenGoblin/RazorBatSpiritCost.prototype
        {  10162u, "X-23" },  // Powers/Player/X23/CoupDeGraceEnableHiddenPassive.prototype
        {  10163u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Talents/ObscuringBrimstoneTalent.prototype
        {  10165u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/SpeedRushPhoenixProcEffect.prototype
        {  10166u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/RavenousBindingSlowEffectTransfering.prototype
        {  10167u, "Ghost Rider" },  // Powers/Player/GhostRider/ChainShockwave.prototype
        {  10168u, "Hawkeye" },  // Powers/Player/Hawkeye/ThreeRoundBurstExtraShot.prototype
        {  10170u, "Winter Soldier" },  // Powers/Player/WinterSoldier/PistolShotMissileEffect.prototype
        {  10172u, "Jean Grey" },  // Powers/Player/JeanGrey/UltimateHotspotEffectPhx.prototype
        {  10176u, "Psylocke" },  // Powers/Player/Psylocke/KatanaLeapSlashAoEDecoy.prototype
        {  10177u, "Vision" },  // Powers/Player/Vision/SolarEnergyPowerCostModifierCondition.prototype
        {  10184u, "Dr. Doom" },  // Powers/Player/DrDoom/ElectricBlast.prototype
        {  10187u, "Nova" },  // Powers/Player/Nova/HeavyBlastBuffFromPulsarTalent.prototype
        {  10188u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/ColleenWing/ColleenWingDeathFromAboveCombo.prototype
        {  10189u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SunspotPunchProc.prototype
        {  10196u, "Nightcrawler" },  // Powers/Player/Nightcrawler/ValiantLeapEnd.prototype
        {  10200u, "War Machine" },  // Powers/Player/WarMachine/ChaingunBurstMissileEffectThermite.prototype
        {  10201u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/SquirrelGirlMeleeSquirrelConeMissileEffect.prototype
        {  10203u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseToggleConditionRemovalA.prototype
        {  10207u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveDaredevilComboPointHighlightImpact.prototype
        {  10210u, "Cable" },  // Powers/Player/Cable/GreymalkinBombSummon.prototype
        {  10211u, "Black Widow" },  // Powers/Player/BlackWidow/RollingGrenades7.prototype
        {  10212u, "Nick Fury" },  // Powers/Player/NickFury/Talents/Talent5SignatureFireteamSteroid.prototype
        {  10214u, "Rogue" },  // Powers/Player/Rogue/UltimateSeekerButterflies.prototype
        {  10215u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaoticDebuffRandomizer.prototype
        {  10216u, "Black Bolt" },  // Powers/Player/BlackBolt/Talents/Talent3SwoopingStrikesBoost.prototype
        {  10218u, "Ghost Rider" },  // Powers/Player/GhostRider/FearCleanseCCImmuneCombo.prototype
        {  10219u, "Nova" },  // Powers/Player/Nova/LungingPunch.prototype
        {  10220u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BrimstoneBeatdownSecondHit.prototype
        {  10221u, "Venom" },  // Powers/Player/Venom/Talents/IchorCostIncrease.prototype
        {  10224u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/ForcePushJeanWeakenEffect.prototype
        {  10225u, "Cable" },  // Powers/Player/Cable/PsimitarWaves.prototype
        {  10226u, "Ghost Rider" },  // Powers/Player/GhostRider/PassiveTeamDefenseProcEffect.prototype
        {  10230u, "Nightcrawler" },  // Powers/Player/Nightcrawler/SwordPummel3rdAttack.prototype
        {  10232u, "Cyclops" },  // Powers/Player/Cyclops/Ultimate.prototype
        {  10233u, "Nick Fury" },  // Powers/Player/NickFury/AutoRevive.prototype
        {  10234u, "Ghost Rider" },  // Powers/Player/GhostRider/ChainRoot.prototype
        {  10236u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent1IncreaseFinisherDamage.prototype
        {  10238u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SecondaryResourceReset.prototype
        {  10240u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickQuiverComboTaserArrowProc.prototype
        {  10244u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/RogueIgnorePainUpdateProc.prototype
        {  10246u, "Black Bolt" },  // Powers/Player/BlackBolt/PBAoE.prototype
        {  10247u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/Pummel4thAttack.prototype
        {  10249u, "Luke Cage" },  // Powers/Player/LukeCage/MeleePunchUppercut.prototype
        {  10251u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GrootOut.prototype
        {  10252u, "Iron Man" },  // Powers/Player/IronMan/MicromissilesMissileEffect.prototype
        {  10254u, "Thor" },  // Powers/Player/Thor/ImmortalCombatProcEffect.prototype
        {  10256u, "Green Goblin" },  // Powers/Player/GreenGoblin/TheBigOneSummon.prototype
        {  10258u, "Deadpool" },  // Powers/Player/Deadpool/Rework/ServerLagBuffComboEffect.prototype
        {  10260u, "Dr. Doom" },  // Powers/Player/DrDoom/AoEDebuffDoTCombo.prototype
        {  10262u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BoardDashEffect.prototype
        {  10266u, "War Machine" },  // Powers/Player/WarMachine/AlphaStrikeMissileEffect.prototype
        {  10268u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceVisualConditionRemoval.prototype
        {  10271u, "Iron Fist" },  // Powers/Player/IronFist/ChiHarmony.prototype
        {  10273u, "Deadpool" },  // Powers/Player/Deadpool/Talents/OrbHealTalent.prototype
        {  10277u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveKurseSelfRezCDDisplay.prototype
        {  10278u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveBatrocProcEffect.prototype
        {  10282u, "Ghost Rider" },  // Powers/Player/GhostRider/Talents/Talent1ChainsAblaze.prototype
        {  10285u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/CarThrow.prototype
        {  10286u, "Magik" },  // Powers/Player/Magik/BounceStrikeEnd.prototype
        {  10287u, "Iron Man" },  // Powers/Player/IronMan/Micromissiles.prototype
        {  10288u, "X-23" },  // Powers/Player/X23/FuriousLungeFuryGain.prototype
        {  10290u, "Magneto" },  // Powers/Player/Magneto/UltimateImplosionMarkerSummon.prototype
        {  10292u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ScarletWitchShadowBolt.prototype
        {  10293u, "Daredevil" },  // Powers/Player/Daredevil/Talents/SlowComboPointTalent.prototype
        {  10295u, "Daredevil" },  // Powers/Player/Daredevil/Update/ClubRicochetMissileEffect.prototype
        {  10296u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosBlast.prototype
        {  10298u, "Dr. Doom" },  // Powers/Player/DrDoom/Talents/Talent1DoombotFlyers.prototype
        {  10299u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/StealthToggle.prototype
        {  10300u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassivePunisher.prototype
        {  10301u, "Punisher" },  // Powers/Player/Punisher/Rework/AutomaticShotgun.prototype
        {  10302u, "Gambit" },  // Powers/Player/Gambit/JacksOrBetterConditionCombo.prototype
        {  10303u, "Black Panther" },  // Powers/Player/BlackPanther/MineFieldExplosionMelee.prototype
        {  10305u, "Thor" },  // Powers/Player/Thor/Talents/ForAsgardWrathOfGod.prototype
        {  10309u, "Moon Knight" },  // Powers/Player/MoonKnight/ConeYankWeakenCombo.prototype
        {  10311u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/UltimateNoMoreBuffCombo.prototype
        {  10318u, "Loki" },  // Powers/Player/Loki/ExplodeIllusionsSpiritRestore.prototype
        {  10322u, "Thing" },  // Powers/Player/Thing/Rework/HotheadDoT.prototype
        {  10323u, "Black Bolt" },  // Powers/Player/BlackBolt/MasterBlowSignaturePunchCombo.prototype
        {  10326u, "Taskmaster" },  // Powers/Player/Taskmaster/SerumGainMechanic.prototype
        {  10329u, "Black Cat" },  // Powers/Player/BlackCat/ClawSwipes.prototype
        {  10330u, "Black Bolt" },  // Powers/Player/BlackBolt/HypersonicScream.prototype
        {  10334u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveKurseSelfRezInvulnerable.prototype
        {  10335u, "Thing" },  // Powers/Player/Thing/Talents/Talent2ClobberinBoost.prototype
        {  10336u, "Beast" },  // Powers/Player/Beast/ElectroGadgetKillShieldGadgets.prototype
        {  10340u, "Hawkeye" },  // Powers/Player/Hawkeye/FlashBomb.prototype
        {  10342u, "Angela" },  // Powers/Player/Angela/MiraculousAssaultStart.prototype
        {  10345u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ExpandingPBAoEEffectLarger.prototype
        {  10346u, "She-Hulk" },  // Powers/Player/SheHulk/BarExamBarThrowMissileEffect.prototype
        {  10350u, "Iceman" },  // Powers/Player/Iceman/IceBlockHiddenPassive.prototype
        {  10351u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/UnmakeRealityChaosCostReductionBuff.prototype
        {  10353u, "Kitty Pryde" },  // Powers/Player/KittyPryde/BasicMeleeSpiritGainCombo.prototype
        {  10354u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BasicBouncingBeamChainEffect.prototype
        {  10356u, "Magneto" },  // Powers/Player/Magneto/UltimateImplosionPullEffect.prototype
        {  10360u, "Kitty Pryde" },  // Powers/Player/KittyPryde/MovementSlash.prototype
        {  10361u, "Hawkeye" },  // Powers/Player/Hawkeye/TurretArrow.prototype
        {  10363u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BoardDash.prototype
        {  10364u, "Colossus" },  // Powers/Player/Colossus/NightcrawlerSummon/DefaultAttack2.prototype
        {  10371u, "Nova" },  // Powers/Player/Nova/Traits/MechanicTrait.prototype
        {  10372u, "Rogue" },  // Powers/Player/Rogue/GlovesOffHealProc.prototype
        {  10376u, "Hawkeye" },  // Powers/Player/Hawkeye/ShriekingArrowTaunt.prototype
        {  10378u, "Black Panther" },  // Powers/Player/BlackPanther/FreezingDaggersMissileEffect.prototype
        {  10382u, "Psylocke" },  // Powers/Player/Psylocke/PsiBoltEnduranceGain.prototype
        {  10384u, "She-Hulk" },  // Powers/Player/SheHulk/LawyerUp.prototype
        {  10386u, "Storm" },  // Powers/Player/Storm/Talents/HurricaneWinds.prototype
        {  10389u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DamageMaelstrom.prototype
        {  10390u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/SquirrelAttackBonus.prototype
        {  10393u, "Beast" },  // Powers/Player/Beast/TeslaTowerGadget.prototype
        {  10397u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/AngelDeathFromAbove.prototype
        {  10398u, "War Machine" },  // Powers/Player/WarMachine/AutogunRecurringEffect.prototype
        {  10400u, "Gambit" },  // Powers/Player/Gambit/UltimateCardThrow3.prototype
        {  10401u, "Dr. Doom" },  // Powers/Player/DrDoom/ChanneledBeam.prototype
        {  10402u, "Punisher" },  // Powers/Player/Punisher/Rework/PineappleGrenadeLauncher.prototype
        {  10403u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallBeastMelee.prototype
        {  10404u, "Rogue" },  // Powers/Player/Rogue/UltimateDashSlash3.prototype
        {  10408u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/GiantGunBonus.prototype
        {  10419u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/MinigunSelfAudio.prototype
        {  10426u, "Jean Grey" },  // Powers/Player/JeanGrey/PhoenixModeToggleRezHealEffect.prototype
        {  10430u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LokiIllusionRushSummonCombo.prototype
        {  10431u, "Nova" },  // Powers/Player/Nova/PassiveSRShield.prototype
        {  10432u, "Hawkeye" },  // Powers/Player/Hawkeye/AdamantiumArrowInstantCast.prototype
        {  10433u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/UnmakeRealityEndChaosDamageBuff.prototype
        {  10434u, "Black Cat" },  // Powers/Player/BlackCat/TumbleStealthTalented.prototype
        {  10435u, "Carnage" },  // Powers/Player/Carnage/MeleeSwordSpin.prototype
        {  10436u, "She-Hulk" },  // Powers/Player/SheHulk/UltimateCCImmuneCombo.prototype
        {  10438u, "Hulk" },  // Powers/Player/Hulk/Rework/PassiveToughHiddenPassive.prototype
        {  10441u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GhostRiderFireBreathHotspotEffec.prototype
        {  10443u, "Hawkeye" },  // Powers/Player/Hawkeye/Talents/ThreeRoundBurstBonusCharge.prototype
        {  10444u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ForcePillar.prototype
        {  10446u, "Daredevil" },  // Powers/Player/Daredevil/Update/BrutalStrikeEffect.prototype
        {  10447u, "Blade" },  // Powers/Player/Blade/SerumInjectionPreventBloodlustCombo.prototype
        {  10450u, "Cable" },  // Powers/Player/Cable/GreymalkinSummonBeamHotspot.prototype
        {  10452u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallJeanDamageMultCombo.prototype
        {  10455u, "Luke Cage" },  // Powers/Player/LukeCage/SummonJessicaJonesCombo.prototype
        {  10456u, "Beast" },  // Powers/Player/Beast/AngelDFAEnd.prototype
        {  10457u, "Cyclops" },  // Powers/Player/Cyclops/Rework/TacticalAnalysisHiddenPassive.prototype
        {  10459u, "Rogue" },  // Powers/Player/Rogue/RecallOverloadPrepareEndExplosion.prototype
        {  10460u, "Colossus" },  // Powers/Player/Colossus/MetalRegenerationComboKnockdown.prototype
        {  10461u, "Thor" },  // Powers/Player/Thor/Rework/OFGainOverTimeCombo30PerSec.prototype
        {  10463u, "Moon Knight" },  // Powers/Player/MoonKnight/BrutalChanceTerrifyHealthCost.prototype
        {  10466u, "Jean Grey" },  // Powers/Player/JeanGrey/PhoenixForceHiddenPassive.prototype
        {  10467u, "Venom" },  // Powers/Player/Venom/FuriousLungeMissileCombo.prototype
        {  10471u, "Cyclops" },  // Powers/Player/Cyclops/TacticalAnalysisXPHotspotEffect.prototype
        {  10474u, "Nightcrawler" },  // Powers/Player/Nightcrawler/StealthHealCombo.prototype
        {  10476u, "Nova" },  // Powers/Player/Nova/PassiveSpeedMeleeBuffEffect.prototype
        {  10482u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/SquirrelArmy.prototype
        {  10483u, "Winter Soldier" },  // Powers/Player/WinterSoldier/PistolShotEnduranceGainBionic.prototype
        {  10486u, "Colossus" },  // Powers/Player/Colossus/ArmoringPunch.prototype
        {  10487u, "Green Goblin" },  // Powers/Player/GreenGoblin/FlightBombs.prototype
        {  10489u, "Psylocke" },  // Powers/Player/Psylocke/KatanaLeapSlashAoE.prototype
        {  10490u, "Human Torch" },  // Powers/Player/HumanTorch/BasicFireball.prototype
        {  10495u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerDeadpoolTheKidGatlingGun.prototype
        {  10497u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BamfYank.prototype
        {  10498u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveKingpinProcEffect.prototype
        {  10500u, "Black Widow" },  // Powers/Player/BlackWidow/ConductiveGrenadeProcEffect.prototype
        {  10502u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateProcComboShellAngel.prototype
        {  10507u, "Storm" },  // Powers/Player/Storm/MicroburstFreezeCombo.prototype
        {  10511u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotBlockadeHiddenPassive.prototype
        {  10514u, "Hulk" },  // Powers/Player/Hulk/UltimateInvulnerableCombo.prototype
        {  10515u, "Venom" },  // Powers/Player/Venom/HealthPassiveSelfRez.prototype
        {  10517u, "Thing" },  // Powers/Player/Thing/Traits/ClobberinTimeDecay.prototype
        {  10518u, "Green Goblin" },  // Powers/Player/GreenGoblin/RazorBatsMissileEffectReturning.prototype
        {  10520u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/NeuralNetworkPhoenixAOEProc.prototype
        {  10523u, "Blade" },  // Powers/Player/Blade/StakeThrowerDoT.prototype
        {  10524u, "Cable" },  // Powers/Player/Cable/Talents/SweepLayer.prototype
        {  10526u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/SpikedBallChanneledHotspotEffect.prototype
        {  10527u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CloseCombatVulnCombo.prototype
        {  10528u, "Iron Man" },  // Powers/Player/IronMan/UltimateSuitBasicPunch4.prototype
        {  10532u, "Thing" },  // Powers/Player/Thing/Rework/PassiveCDROnOrbPickup.prototype
        {  10535u, "Storm" },  // Powers/Player/Storm/ChainLightning.prototype
        {  10541u, "Beast" },  // Powers/Player/Beast/DeathFromAboveDamageShieldCombo.prototype
        {  10542u, "Iceman" },  // Powers/Player/Iceman/IceGolemTaunt.prototype
        {  10543u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/EnergyTurretShot.prototype
        {  10544u, "Iron Fist" },  // Powers/Player/IronFist/Talents/Talent4HarmonyChi.prototype
        {  10550u, "Green Goblin" },  // Powers/Player/GreenGoblin/SignatureRegen.prototype
        {  10551u, "Hulk" },  // Powers/Player/Hulk/HulkFastProcEffect.prototype
        {  10553u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/DarkHexRavenousBindingFilterPower.prototype
        {  10557u, "Iron Man" },  // Powers/Player/IronMan/BrutalStrikeWeakenCombo.prototype
        {  10559u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AmpControlledMobBeginExplosion.prototype
        {  10563u, "Storm" },  // Powers/Player/Storm/Talents/ColdDamageSpec.prototype
        {  10567u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/RemoveGrootSummon.prototype
        {  10568u, "Colossus" },  // Powers/Player/Colossus/Traits/ArmorRegenPauseTrigger.prototype
        {  10569u, "Cyclops" },  // Powers/Player/Cyclops/Rework/PrismBeam.prototype
        {  10570u, "Angela" },  // Powers/Player/Angela/RibbonBonusDamageCombo.prototype
        {  10571u, "Black Panther" },  // Powers/Player/BlackPanther/FreezingDaggersSlow.prototype
        {  10572u, "Juggernaut" },  // Powers/Player/Juggernaut/UnstoppableChargeShorter.prototype
        {  10576u, "Cable" },  // Powers/Player/Cable/TelepathicIllusionAttackProc.prototype
        {  10577u, "Jean Grey" },  // Powers/Player/JeanGrey/UltimateHiddenPassive.prototype
        {  10578u, "Kitty Pryde" },  // Powers/Player/KittyPryde/SignatureLonger.prototype
        {  10579u, "Nick Fury" },  // Powers/Player/NickFury/Talents/Talent2FireteamShotgun.prototype
        {  10582u, "Deadpool" },  // Powers/Player/Deadpool/Rework/GiantMalletExplosion.prototype
        {  10588u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/BasicPistolsMissileEffect.prototype
        {  10593u, "Magik" },  // Powers/Player/Magik/SummonNastirh.prototype
        {  10597u, "Blade" },  // Powers/Player/Blade/SerumInjectionReviveCooldownDisplay.prototype
        {  10599u, "Black Bolt" },  // Powers/Player/BlackBolt/EnergyGainOverTime3s.prototype
        {  10601u, "Iceman" },  // Powers/Player/Iceman/Talents/FocusBeamToFrostNova.prototype
        {  10605u, "Black Panther" },  // Powers/Player/BlackPanther/EnervationDaggersDoTStack.prototype
        {  10612u, "Iron Man" },  // Powers/Player/IronMan/UltimateSuitRepulsorBurstCollide.prototype
        {  10614u, "Black Widow" },  // Powers/Player/BlackWidow/Traits/ChargeCostCombo175.prototype
        {  10615u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/AmpControlledMobNoKill.prototype
        {  10619u, "She-Hulk" },  // Powers/Player/SheHulk/LawyerUpHighlight.prototype
        {  10622u, "Iceman" },  // Powers/Player/Iceman/FrozenOrbCollidePower.prototype
        {  10625u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickArrowGainProc.prototype
        {  10627u, "Hawkeye" },  // Powers/Player/Hawkeye/Talents/ShriekingArrowTalent.prototype
        {  10628u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveStarLord.prototype
        {  10630u, "Rogue" },  // Powers/Player/Rogue/DrainLife.prototype
        {  10633u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/UltimateNoMoreEndExplosion.prototype
        {  10635u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateJeanImplosionMoveCombo.prototype
        {  10638u, "Black Widow" },  // Powers/Player/BlackWidow/PistolShot.prototype
        {  10639u, "Magneto" },  // Powers/Player/Magneto/AutoDebrisCrushProcEffect.prototype
        {  10645u, "Doctor Strange" },  // Powers/Player/DoctorStrange/VishantiSealExplosion.prototype
        {  10646u, "Ghost Rider" },  // Powers/Player/GhostRider/Talents/Talent5HellfireBeam.prototype
        {  10650u, "Blade" },  // Powers/Player/Blade/UVGrenadeDamageComboTalent.prototype
        {  10653u, "Captain America" },  // Powers/Player/CaptainAmerica/FinestHourToughnessBuff.prototype
        {  10657u, "Hawkeye" },  // Powers/Player/Hawkeye/BasicArrowMissileEffect.prototype
        {  10659u, "Thing" },  // Powers/Player/Thing/Rework/GroundSmashBigger.prototype
        {  10662u, "Moon Knight" },  // Powers/Player/MoonKnight/UltimateInvulnCombo.prototype
        {  10665u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/JuggernautImInvulnerableMomentumStartGaining.prototype
        {  10666u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LokiIllusionMeleeAttack3.prototype
        {  10667u, "Beast" },  // Powers/Player/Beast/GlueBombAreaSummon.prototype
        {  10671u, "Loki" },  // Powers/Player/Loki/IllusionCounterHiddenPassive.prototype
        {  10673u, "Juggernaut" },  // Powers/Player/Juggernaut/ImInvulnerableCooldownDisplay.prototype
        {  10674u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/BurningProc.prototype
        {  10675u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GhostRiderFireBreath.prototype
        {  10676u, "Loki" },  // Powers/Player/Loki/SoulCrushTransfer.prototype
        {  10678u, "Rogue" },  // Powers/Player/Rogue/Talents/NonStolenPowersBuff.prototype
        {  10683u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/PassiveSquirrelPetsProcEffectExtraFive.prototype
        {  10686u, "Black Bolt" },  // Powers/Player/BlackBolt/BigBurst.prototype
        {  10688u, "Dr. Doom" },  // Powers/Player/DrDoom/Teleport.prototype
        {  10691u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveAngela.prototype
        {  10692u, "Loki" },  // Powers/Player/Loki/UltimateChargeEffect.prototype
        {  10693u, "Doctor Strange" },  // Powers/Player/DoctorStrange/EyeOfAgamotto.prototype
        {  10695u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/BouncyBallRemoveCondition.prototype
        {  10697u, "Luke Cage" },  // Powers/Player/LukeCage/UnbreakableSkinReflectVisual.prototype
        {  10699u, "Psylocke" },  // Powers/Player/Psylocke/SeekerButterfliesMissileEffect.prototype
        {  10704u, "Magik" },  // Powers/Player/Magik/BoneWallKnockback.prototype
        {  10705u, "Gambit" },  // Powers/Player/Gambit/RoyalFlushRangedBuffCombo.prototype
        {  10713u, "Blade" },  // Powers/Player/Blade/AdvancedTechniqueCombo2Spin2.prototype
        {  10715u, "Ultron" },  // Powers/Player/Ultron/SuicideDroneSelfRez.prototype
        {  10716u, "Elektra" },  // Powers/Player/Elektra/ShadowStrikeShowMesh.prototype
        {  10717u, "Hulk" },  // Powers/Player/Hulk/Traits/OffenseTrait.prototype
        {  10718u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosBlastBuffFromIronMaiden.prototype
        {  10726u, "Magik" },  // Powers/Player/Magik/NastirhInfect.prototype
        {  10729u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ExpandingPBAoESelfHealing.prototype
        {  10743u, "Magik" },  // Powers/Player/Magik/Talents/Talent5DarkPactIntoDarkAlliance.prototype
        {  10748u, "Doctor Strange" },  // Powers/Player/DoctorStrange/MirrorImageMissileEffectPlaceholder.prototype
        {  10749u, "Cyclops" },  // Powers/Player/Cyclops/BouncingBeamChainEffect.prototype
        {  10752u, "Thing" },  // Powers/Player/Thing/Talents/Talent5WeaponsBuff.prototype
        {  10754u, "Loki" },  // Powers/Player/Loki/Talents/FourRealmsEternalDarkness.prototype
        {  10756u, "Hawkeye" },  // Powers/Player/Hawkeye/Talents/TurretArrowBonusArrowTalent.prototype
        {  10757u, "Hulk" },  // Powers/Player/Hulk/HighlightRampage.prototype
        {  10758u, "X-23" },  // Powers/Player/X23/Talents/Talent1WrathMvmtDmg.prototype
        {  10759u, "Nova" },  // Powers/Player/Nova/Talents/Talent1MovementExplosion.prototype
        {  10763u, "Deadpool" },  // Powers/Player/Deadpool/Rework/Deadpoolnado.prototype
        {  10764u, "Ghost Rider" },  // Powers/Player/GhostRider/ChainRootBasicFirebalDoTStack.prototype
        {  10765u, "Nova" },  // Powers/Player/Nova/ChanneledBeamEnhancedStackingBuff2.prototype
        {  10766u, "Ant-Man" },  // Powers/Player/AntMan/FlyingAntSwarmSlowHotspotEffect.prototype
        {  10767u, "Hulk" },  // Powers/Player/Hulk/ChargeCollisionEffect.prototype
        {  10768u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/RavenousBindingMissileEffect.prototype
        {  10769u, "Doctor Strange" },  // Powers/Player/DoctorStrange/WindsOfWatoomb.prototype
        {  10770u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Traits/DefenseTrait.prototype
        {  10773u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/PsylockePsyBowMissileEffect.prototype
        {  10774u, "Vision" },  // Powers/Player/Vision/Talents/Talent4RobotPetSolarBuff.prototype
        {  10777u, "Hulk" },  // Powers/Player/Hulk/ClapCDRFilterPower.prototype
        {  10778u, "Beast" },  // Powers/Player/Beast/BeastBamf.prototype
        {  10780u, "Deadpool" },  // Powers/Player/Deadpool/Rework/StabbyFlip.prototype
        {  10785u, "Black Widow" },  // Powers/Player/BlackWidow/PunchWeakenEffect.prototype
        {  10789u, "Nick Fury" },  // Powers/Player/NickFury/HeadsDownInvuln.prototype
        {  10797u, "Rogue" },  // Powers/Player/Rogue/StolenPowerLibrarySlot2.prototype
        {  10798u, "Nova" },  // Powers/Player/Nova/HeavyBlastHiddenPassive.prototype
        {  10799u, "Luke Cage" },  // Powers/Player/LukeCage/PunchTheGround.prototype
        {  10801u, "Blade" },  // Powers/Player/Blade/Traits/AmmoRegenTrigger.prototype
        {  10808u, "Thing" },  // Powers/Player/Thing/KnockdownChargeSprint.prototype
        {  10812u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/DeadpoolHybridTalent.prototype
        {  10815u, "Iceman" },  // Powers/Player/Iceman/CleanseIceNova.prototype
        {  10816u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerHydraBobMissilePower.prototype
        {  10821u, "Thing" },  // Powers/Player/Thing/Rework/CallHothead.prototype
        {  10823u, "Rogue" },  // Powers/Player/Rogue/UltimateSwordFlurryBuffEffect.prototype
        {  10829u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Talents/FlourishDeflectChanceTalent.prototype
        {  10830u, "Black Widow" },  // Powers/Player/BlackWidow/WidowsBite.prototype
        {  10833u, "Juggernaut" },  // Powers/Player/Juggernaut/Headbutt.prototype
        {  10840u, "Human Torch" },  // Powers/Player/HumanTorch/ConsumeHitFX.prototype
        {  10841u, "Iceman" },  // Powers/Player/Iceman/Traits/FrostArmorDamageAbsorbStopper.prototype
        {  10844u, "War Machine" },  // Powers/Player/WarMachine/AlphaStrikeHotspotEffect.prototype
        {  10849u, "Human Torch" },  // Powers/Player/HumanTorch/ProtectiveFlames.prototype
        {  10851u, "Ultron" },  // Powers/Player/Ultron/DroneExplodeonDeathProc.prototype
        {  10852u, "Iron Fist" },  // Powers/Player/IronFist/UltimateSpiritofShouLouEffect.prototype
        {  10858u, "Nick Fury" },  // Powers/Player/NickFury/EyesEverywhere.prototype
        {  10859u, "Cyclops" },  // Powers/Player/Cyclops/CallMagik.prototype
        {  10861u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent2DefenseBuffGuardedAllies.prototype
        {  10863u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/HealthDefenseSelfRezEffect.prototype
        {  10865u, "Punisher" },  // Powers/Player/Punisher/Traits/MechanicTraitAmmo.prototype
        {  10870u, "Black Panther" },  // Powers/Player/BlackPanther/SmokeScreen.prototype
        {  10871u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DamageCone.prototype
        {  10873u, "Silver Surfer" },  // Powers/Player/SilverSurfer/DeconstructionResourceGain.prototype
        {  10874u, "Deadpool" },  // Powers/Player/Deadpool/HealthRegenHiddenPassive.prototype
        {  10888u, "Venom" },  // Powers/Player/Venom/FuriousLungeStart.prototype
        {  10890u, "Beast" },  // Powers/Player/Beast/CloseGapBleedCombo.prototype
        {  10895u, "Ultron" },  // Powers/Player/Ultron/YankSlamDamageMultCombo.prototype
        {  10896u, "Iron Man" },  // Powers/Player/IronMan/OrbitalBombardmentRandomStrikeProcEffect.prototype
        {  10900u, "Ghost Rider" },  // Powers/Player/GhostRider/FirePillarBasicFireballDoTStack.prototype
        {  10903u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/MysticismNoReset.prototype
        {  10904u, "Black Widow" },  // Powers/Player/BlackWidow/RapidShotSelfAudioCombo.prototype
        {  10907u, "Blade" },  // Powers/Player/Blade/SerumInjectionAutoRevive.prototype
        {  10910u, "Taskmaster" },  // Powers/Player/Taskmaster/BoomerangArrow.prototype
        {  10911u, "Cyclops" },  // Powers/Player/Cyclops/Rework/ChargeConeHiddenPassiveCDRProc.prototype
        {  10914u, "Angela" },  // Powers/Player/Angela/RibbonDancerHotspotEffect.prototype
        {  10915u, "Taskmaster" },  // Powers/Player/Taskmaster/SwordStrikeTwoEnduranceGain.prototype
        {  10917u, "X-23" },  // Powers/Player/X23/Traits/DefenseTrait.prototype
        {  10919u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/LukeCageComboTalent.prototype
        {  10922u, "Iron Fist" },  // Powers/Player/IronFist/BlackBlackPoisonTouch.prototype
        {  10924u, "Colossus" },  // Powers/Player/Colossus/ShockwaveTremorsHotspotEffect.prototype
        {  10928u, "Kitty Pryde" },  // Powers/Player/KittyPryde/BasicSlash.prototype
        {  10933u, "Hawkeye" },  // Powers/Player/Hawkeye/Talents/DisengagingShotTalent.prototype
        {  10934u, "Iron Fist" },  // Powers/Player/IronFist/Talents/Talent2ComboFlow.prototype
        {  10935u, "Iron Fist" },  // Powers/Player/IronFist/TigerStanceEnduranceMaterialOverride.prototype
        {  10936u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/DoctorStrangeFangNukeMissileEffe.prototype
        {  10939u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent3ConvictionCDR.prototype
        {  10941u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/RangedSignature.prototype
        {  10945u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/Pummel2ndAttack.prototype
        {  10948u, "Doctor Strange" },  // Powers/Player/DoctorStrange/MysticEnergyGainMechanic.prototype
        {  10951u, "Rogue" },  // Powers/Player/Rogue/UltimateTransformComboDeactivate.prototype
        {  10952u, "Iron Man" },  // Powers/Player/IronMan/UltimateSuitRepulsorBurst.prototype
        {  10953u, "Deadpool" },  // Powers/Player/Deadpool/Talents/LilDeadpoolTalent.prototype
        {  10955u, "Magneto" },  // Powers/Player/Magneto/MetalObjectSmash.prototype
        {  10957u, "Iceman" },  // Powers/Player/Iceman/FocusBeam.prototype
        {  10958u, "Taskmaster" },  // Powers/Player/Taskmaster/Volley.prototype
        {  10959u, "Hulk" },  // Powers/Player/Hulk/GetAngryGainEffect.prototype
        {  10961u, "Human Torch" },  // Powers/Player/HumanTorch/ConsumeSelfHealing.prototype
        {  10962u, "Magik" },  // Powers/Player/Magik/SummonLimboDemon.prototype
        {  10964u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BlobBellyFlop.prototype
        {  10969u, "Thor" },  // Powers/Player/Thor/Rework/HammerDash.prototype
        {  10972u, "Captain America" },  // Powers/Player/CaptainAmerica/PBAoEDamageMultCondition.prototype
        {  10974u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCardMissileEffectStage2.prototype
        {  10980u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/BubbleSprayKnockbackCombo.prototype
        {  10981u, "Iron Fist" },  // Powers/Player/IronFist/KunlunStrike.prototype
        {  10983u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotInfernoHiddenPassive.prototype
        {  10987u, "Juggernaut" },  // Powers/Player/Juggernaut/MomentumStartGaining.prototype
        {  10989u, "Nightcrawler" },  // Powers/Player/Nightcrawler/TeleportStealthCombo.prototype
        {  10991u, "Daredevil" },  // Powers/Player/Daredevil/Update/ComboPointGainMechanic.prototype
        {  10993u, "Daredevil" },  // Powers/Player/Daredevil/ClubRicochetBleedMissileEffect.prototype
        {  10995u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/StepfordUltimateHotspotSlowEffect.prototype
        {  10996u, "Punisher" },  // Powers/Player/Punisher/FlamethrowerHotspotEffectDoT.prototype
        {  10998u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateJeanTKTossEffect.prototype
        {  11000u, "Venom" },  // Powers/Player/Venom/Traits/MechanicTraitIchor.prototype
        {  11004u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/DarkPhoenixPassive.prototype
        {  11005u, "Deadpool" },  // Powers/Player/Deadpool/Talents/SmellsLikeVictoryRemovalTimer.prototype
        {  11007u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ExpandingPBAoE.prototype
        {  11008u, "Wolverine" },  // Powers/Player/Wolverine/Talents/Talent2PBAoEAddBleed.prototype
        {  11009u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/FlashGrenadeConductiveGrenade.prototype
        {  11010u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/NickFuryPetSteroidBuffCombo.prototype
        {  11013u, "Daredevil" },  // Powers/Player/Daredevil/UltimateShadowStrike.prototype
        {  11022u, "Green Goblin" },  // Powers/Player/GreenGoblin/DashBombs.prototype
        {  11023u, "Cyclops" },  // Powers/Player/Cyclops/Rework/ChargeConeHiddenPassive.prototype
        {  11024u, "Doctor Strange" },  // Powers/Player/DoctorStrange/ProjectionBasicDaggersMissileEff.prototype
        {  11026u, "Daredevil" },  // Powers/Player/Daredevil/Update/ClubAttackEnduranceRegen.prototype
        {  11029u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PullUnderBosses.prototype
        {  11031u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveMoonKnightHealthMinEffect.prototype
        {  11034u, "Winter Soldier" },  // Powers/Player/WinterSoldier/SpinningMinesImplosion.prototype
        {  11036u, "Loki" },  // Powers/Player/Loki/EnchantmentLight.prototype
        {  11039u, "Nova" },  // Powers/Player/Nova/BouncingStrikeChainPower.prototype
        {  11040u, "Black Bolt" },  // Powers/Player/BlackBolt/Barrier.prototype
        {  11045u, "Wolverine" },  // Powers/Player/Wolverine/RapidRegenerationLowHealthProc.prototype
        {  11047u, "Green Goblin" },  // Powers/Player/GreenGoblin/GoblinLaserCharging.prototype
        {  11049u, "Iron Man" },  // Powers/Player/IronMan/Talents/MissileTargeting.prototype
        {  11054u, "Carnage" },  // Powers/Player/Carnage/TransfusionPBAoEOneShot.prototype
        {  11055u, "Deadpool" },  // Powers/Player/Deadpool/Rework/PowerUpTeddyEffect.prototype
        {  11060u, "Ant-Man" },  // Powers/Player/AntMan/AntPunch.prototype
        {  11064u, "Cable" },  // Powers/Player/Cable/PlasmaBarrageHotspotEffect.prototype
        {  11065u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent4PsionicBow.prototype
        {  11073u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/KineticBoltJean.prototype
        {  11074u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/CircularLogicCDRProc.prototype
        {  11075u, "Venom" },  // Powers/Player/Venom/ConeDrainHotspotEffect.prototype
        {  11078u, "Elektra" },  // Powers/Player/Elektra/BasicSaiHealthOnHit.prototype
        {  11080u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedEnergyPauseProc.prototype
        {  11081u, "Iron Man" },  // Powers/Player/IronMan/BasicDentingPunch.prototype
        {  11084u, "Hulk" },  // Powers/Player/Hulk/Rework/DashCrashRouter.prototype
        {  11085u, "Ghost Rider" },  // Powers/Player/GhostRider/ChainsAblazeCombo.prototype
        {  11092u, "Wolverine" },  // Powers/Player/Wolverine/Talents/Talent5CantKeepMeDown.prototype
        {  11095u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GrootSummonCombo.prototype
        {  11097u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/FocusGainMechanicSR.prototype
        {  11101u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/FirestarEnergyRain.prototype
        {  11102u, "Magik" },  // Powers/Player/Magik/DarkPact.prototype
        {  11103u, "Beast" },  // Powers/Player/Beast/PummelResetTimer.prototype
        {  11105u, "Jean Grey" },  // Powers/Player/JeanGrey/PhoenixForceGainProc.prototype
        {  11108u, "Luke Cage" },  // Powers/Player/LukeCage/Talents/HeroesForHireBusinessIsGood.prototype
        {  11113u, "Green Goblin" },  // Powers/Player/GreenGoblin/BombingCircleBombDoTCombo.prototype
        {  11114u, "Juggernaut" },  // Powers/Player/Juggernaut/MomentumPunch.prototype
        {  11116u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/BFG.prototype
        {  11118u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/PowerCosmicRegen.prototype
        {  11119u, "Deadpool" },  // Powers/Player/Deadpool/Talents/HulkHandArrowNapalmTalent.prototype
        {  11122u, "Thing" },  // Powers/Player/Thing/Rework/CallStretch.prototype
        {  11124u, "Gambit" },  // Powers/Player/Gambit/FoldEmCombo.prototype
        {  11126u, "Iceman" },  // Powers/Player/Iceman/Talents/SignatureIceGolemBuff.prototype
        {  11132u, "Venom" },  // Powers/Player/Venom/SymbioteDrainPowerGreen1.prototype
        {  11133u, "Luke Cage" },  // Powers/Player/LukeCage/LukeCageSummonEnduranceRegenHPsv.prototype
        {  11136u, "Black Cat" },  // Powers/Player/BlackCat/InstantKillProcEffect.prototype
        {  11138u, "Elektra" },  // Powers/Player/Elektra/Talents/SansetsukonMastery.prototype
        {  11143u, "Iceman" },  // Powers/Player/Iceman/IcicleRanged.prototype
        {  11146u, "Doctor Strange" },  // Powers/Player/DoctorStrange/MysticEnergyOrbVisual4.prototype
        {  11148u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/WarpTurretSummonDeflectHotspot.prototype
        {  11152u, "Daredevil" },  // Powers/Player/Daredevil/Talents/OpenerClubWeakenTalent.prototype
        {  11153u, "War Machine" },  // Powers/Player/WarMachine/Traits/DefenseTrait.prototype
        {  11158u, "Dr. Doom" },  // Powers/Player/DrDoom/Talents/Talent3ChanneledTormentBonus.prototype
        {  11165u, "Beast" },  // Powers/Player/Beast/SleepGasGadgetVulnerabilityHSEffect.prototype
        {  11169u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelBombsHulkbusterEdition.prototype
        {  11170u, "Winter Soldier" },  // Powers/Player/WinterSoldier/TripleShotMissileEffect.prototype
        {  11171u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveRescueProcEffect.prototype
        {  11175u, "Gambit" },  // Powers/Player/Gambit/Talents/Talent5AceOfDiamonds.prototype
        {  11180u, "Storm" },  // Powers/Player/Storm/ChanneledLightningArcBeam.prototype
        {  11184u, "Ghost Rider" },  // Powers/Player/GhostRider/ChargeUpBikeMissileEffect.prototype
        {  11196u, "Black Cat" },  // Powers/Player/BlackCat/Traits/OffenseTrait.prototype
        {  11197u, "Captain America" },  // Powers/Player/CaptainAmerica/UltimateEmpoweredBuffComboEffect.prototype
        {  11198u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/DarkPhoenixPassiveDecay.prototype
        {  11199u, "Hawkeye" },  // Powers/Player/Hawkeye/UltimateBuffCombo.prototype
        {  11202u, "Loki" },  // Powers/Player/Loki/UltimateNornStones.prototype
        {  11203u, "Thing" },  // Powers/Player/Thing/Talents/Talent1CallInSharedCooldown.prototype
        {  11207u, "Iron Man" },  // Powers/Player/IronMan/RepulsorBurstCollidePower.prototype
        {  11208u, "Elektra" },  // Powers/Player/Elektra/SilentScreamIrresistibleStunCombo.prototype
        {  11211u, "Green Goblin" },  // Powers/Player/GreenGoblin/GoblinSerumSelfRez.prototype
        {  11215u, "Human Torch" },  // Powers/Player/HumanTorch/NovaChargeHealEffect.prototype
        {  11216u, "Luke Cage" },  // Powers/Player/LukeCage/PunchTheGroundKnockdownCombo.prototype
        {  11217u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshueStatueTerrifyDebuffHotspotEffect.prototype
        {  11218u, "Nova" },  // Powers/Player/Nova/MegaPunchAoECombo.prototype
        {  11224u, "Winter Soldier" },  // Powers/Player/WinterSoldier/GunStanceCostReductionProc.prototype
        {  11227u, "Luke Cage" },  // Powers/Player/LukeCage/PummelNoFinisherBuffRemoval.prototype
        {  11233u, "Gambit" },  // Powers/Player/Gambit/JacksOrBetterAoEExplosion.prototype
        {  11234u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/Cauterize.prototype
        {  11235u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/DamageConeIronMaiden.prototype
        {  11236u, "Elektra" },  // Powers/Player/Elektra/ShadowStrikeDrop.prototype
        {  11237u, "Luke Cage" },  // Powers/Player/LukeCage/Talents/MobilityBuffs.prototype
        {  11238u, "Psylocke" },  // Powers/Player/Psylocke/AoEDoT.prototype
        {  11240u, "Black Widow" },  // Powers/Player/BlackWidow/CoupDeGraceCombo.prototype
        {  11244u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveSpidermanProcEffect.prototype
        {  11245u, "Dr. Doom" },  // Powers/Player/DrDoom/FootDiveEnd.prototype
        {  11247u, "Wolverine" },  // Powers/Player/Wolverine/Frenzy.prototype
        {  11248u, "Thing" },  // Powers/Player/Thing/CallStretchPassive.prototype
        {  11249u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/NovaBlastoff.prototype
        {  11251u, "Black Cat" },  // Powers/Player/BlackCat/Talents/TalentMasterThiefCrit.prototype
        {  11254u, "Nova" },  // Powers/Player/Nova/Talents/Talent5SigCDPowers.prototype
        {  11255u, "Angela" },  // Powers/Player/Angela/SigNoMatchCooldown.prototype
        {  11257u, "Black Widow" },  // Powers/Player/BlackWidow/Knife.prototype
        {  11259u, "Magik" },  // Powers/Player/Magik/SoulCaptureMinionBuffProjectileMissileEffect.prototype
        {  11260u, "Iron Man" },  // Powers/Player/IronMan/ShieldOverloadExplosion.prototype
        {  11261u, "Storm" },  // Powers/Player/Storm/HailstormHotspotSlowEffect.prototype
        {  11263u, "Elektra" },  // Powers/Player/Elektra/CrossStrikeIntervalEffect.prototype
        {  11264u, "War Machine" },  // Powers/Player/WarMachine/Talents/Talent3ArcReactor.prototype
        {  11266u, "Venom" },  // Powers/Player/Venom/YankDamageBonusCombo.prototype
        {  11269u, "Deadpool" },  // Powers/Player/Deadpool/Rework/SaiAssault.prototype
        {  11273u, "Taskmaster" },  // Powers/Player/Taskmaster/SteroidHotspotBuffCombo.prototype
        {  11274u, "X-23" },  // Powers/Player/X23/BasicMvt.prototype
        {  11276u, "Carnage" },  // Powers/Player/Carnage/CarnageBloodlustResourceRemove.prototype
        {  11279u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/BurningProcEffect.prototype
        {  11280u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/Pummel.prototype
        {  11285u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent1DamageMultWithNoArmor.prototype
        {  11286u, "Ghost Rider" },  // Powers/Player/GhostRider/Traits/OffenseTrait.prototype
        {  11289u, "Nick Fury" },  // Powers/Player/NickFury/DriveByMissileEffect.prototype
        {  11290u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedAutoAttackLockout800MS.prototype
        {  11292u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Talents/Talent4Restoration.prototype
        {  11294u, "Daredevil" },  // Powers/Player/Daredevil/TripleStrike3rdHit.prototype
        {  11297u, "Blade" },  // Powers/Player/Blade/RapidFireHotspotEffect.prototype
        {  11299u, "Nova" },  // Powers/Player/Nova/DeathFromAboveSummonCombo.prototype
        {  11303u, "Elektra" },  // Powers/Player/Elektra/TripleChain.prototype
        {  11304u, "Doctor Strange" },  // Powers/Player/DoctorStrange/VishantiSealMindlessOnePunch.prototype
        {  11305u, "Magik" },  // Powers/Player/Magik/BFLDAoEPunch2.prototype
        {  11307u, "Deadpool" },  // Powers/Player/Deadpool/PowerUpSpeedEffect.prototype
        {  11310u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/AstralFormBonus.prototype
        {  11311u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/CestusPunchLayer.prototype
        {  11312u, "Captain America" },  // Powers/Player/CaptainAmerica/Talents/SerumDoubleSpec.prototype
        {  11314u, "Loki" },  // Powers/Player/Loki/SorcerousShieldVisualEffect.prototype
        {  11315u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/DoubleStrike.prototype
        {  11316u, "Cable" },  // Powers/Player/Cable/Greymalkin.prototype
        {  11317u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateJeanSigAreaEffect.prototype
        {  11319u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Whirlwind.prototype
        {  11323u, "Nova" },  // Powers/Player/Nova/PulsarImplosionProc.prototype
        {  11329u, "Nova" },  // Powers/Player/Nova/UltimateNovaCorps.prototype
        {  11330u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LukeCagePummel2ndAttack.prototype
        {  11335u, "She-Hulk" },  // Powers/Player/SheHulk/Battery.prototype
        {  11337u, "Loki" },  // Powers/Player/Loki/IllusionRushSummonCombo.prototype
        {  11340u, "Magik" },  // Powers/Player/Magik/Talents/Talent2LifeTapWeaken.prototype
        {  11341u, "Iron Fist" },  // Powers/Player/IronFist/LeopardStanceEnduranceMaterialOverride.prototype
        {  11342u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/DefenseHotspotDeflectionEffect.prototype
        {  11343u, "Thor" },  // Powers/Player/Thor/BasicMeleeChainLightningCombo.prototype
        {  11345u, "Magik" },  // Powers/Player/Magik/DarkAlliance.prototype
        {  11347u, "Captain America" },  // Powers/Player/CaptainAmerica/UltimateStartInvuln.prototype
        {  11350u, "Blade" },  // Powers/Player/Blade/SerumInjection.prototype
        {  11351u, "Cable" },  // Powers/Player/Cable/FutureBombEnergyExplosionMentalKeyword.prototype
        {  11353u, "Vision" },  // Powers/Player/Vision/DensePunch.prototype
        {  11356u, "Taskmaster" },  // Powers/Player/Taskmaster/UltimateBlackKnightBuff.prototype
        {  11357u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SlagFireMeteorHotspotEffect.prototype
        {  11358u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedFireBreathHotspotsEnhanced.prototype
        {  11362u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/RemoveArcTurretSummons.prototype
        {  11365u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/HulkbusterSquirrelsEffect.prototype
        {  11366u, "Nova" },  // Powers/Player/Nova/BasicPunchNoCollide.prototype
        {  11368u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/InvisibilityAutoProc.prototype
        {  11369u, "Vision" },  // Powers/Player/Vision/ModeToggleSwitchToPhasePunchBonus.prototype
        {  11374u, "Venom" },  // Powers/Player/Venom/MeleePassive.prototype
        {  11376u, "Human Torch" },  // Powers/Player/HumanTorch/UltimateFirePillarHotspotEffect.prototype
        {  11380u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelBuffsHiddenPassive.prototype
        {  11382u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/AmpControlledMobUnlockPotential.prototype
        {  11383u, "Black Bolt" },  // Powers/Player/BlackBolt/BlackBoltInCombatProc.prototype
        {  11388u, "Dr. Doom" },  // Powers/Player/DrDoom/BasicPunchSpiritGainComboEffect.prototype
        {  11391u, "Kitty Pryde" },  // Powers/Player/KittyPryde/NoCollisionPassive.prototype
        {  11392u, "Thor" },  // Powers/Player/Thor/DeathFromAboveComboEffect.prototype
        {  11393u, "Iron Fist" },  // Powers/Player/IronFist/ChiOverloadAsCombo.prototype
        {  11394u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/UltimateChanneledBeam.prototype
        {  11396u, "Loki" },  // Powers/Player/Loki/SwordSliceHealthOnHit.prototype
        {  11397u, "Iceman" },  // Powers/Player/Iceman/UltimateCloneMeleeDefaultAttack.prototype
        {  11398u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GambitRaginCajunCDR.prototype
        {  11400u, "Vision" },  // Powers/Player/Vision/PhaseHand.prototype
        {  11413u, "Elektra" },  // Powers/Player/Elektra/Talents/NinjaWarriorAllies.prototype
        {  11417u, "Black Widow" },  // Powers/Player/BlackWidow/Ultimate.prototype
        {  11419u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/PassiveSquirrelDodgeProcEffect.prototype
        {  11420u, "Vision" },  // Powers/Player/Vision/Talents/Talent4EnhanceRobotNoDetonation.prototype
        {  11422u, "Gambit" },  // Powers/Player/Gambit/TumbleStunEffect.prototype
        {  11425u, "Deadpool" },  // Powers/Player/Deadpool/Traits/DefenseTrait.prototype
        {  11429u, "Wolverine" },  // Powers/Player/Wolverine/Lunge.prototype
        {  11430u, "Deadpool" },  // Powers/Player/Deadpool/Rework/ArmorBuster.prototype
        {  11431u, "Magneto" },  // Powers/Player/Magneto/HomingBlastMissileEffect.prototype
        {  11433u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/KaeciliusHealChannelEruption.prototype
        {  11436u, "Moon Knight" },  // Powers/Player/MoonKnight/BasicStaffStrikeRestoreCombo.prototype
        {  11439u, "She-Hulk" },  // Powers/Player/SheHulk/HostileWitnessUpgraded.prototype
        {  11441u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SpiderwomanVenomBlastHotspotEffe.prototype
        {  11443u, "Venom" },  // Powers/Player/Venom/SigFreakoutHealthGain.prototype
        {  11448u, "She-Hulk" },  // Powers/Player/SheHulk/ObjectionCeaseAndDesistCombo.prototype
        {  11453u, "Thor" },  // Powers/Player/Thor/Rework/PBAoEStormEffect.prototype
        {  11456u, "Black Widow" },  // Powers/Player/BlackWidow/UltimateSlowEffect.prototype
        {  11461u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveCoulsonProcEffect.prototype
        {  11462u, "Green Goblin" },  // Powers/Player/GreenGoblin/GoblinBlastStackingBuff.prototype
        {  11463u, "Thing" },  // Powers/Player/Thing/Rework/AuraAccuracyComboExclusive.prototype
        {  11465u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TelepathicIllusionPhoenixXmenSummon.prototype
        {  11470u, "Loki" },  // Powers/Player/Loki/Talents/FourRealmsDarkBolt.prototype
        {  11474u, "Daredevil" },  // Powers/Player/Daredevil/CaneAttackSlow.prototype
        {  11475u, "Iceman" },  // Powers/Player/Iceman/DeepFreezeFilterPowerIceBlock.prototype
        {  11476u, "Beast" },  // Powers/Player/Beast/CloseGap.prototype
        {  11477u, "Colossus" },  // Powers/Player/Colossus/Traits/OffenseTrait.prototype
        {  11484u, "Magik" },  // Powers/Player/Magik/BFLDSummonCombo.prototype
        {  11485u, "War Machine" },  // Powers/Player/WarMachine/ThermalShotDoTCombo.prototype
        {  11487u, "Magneto" },  // Powers/Player/Magneto/Talents/AllInPickupBuff.prototype
        {  11491u, "Carnage" },  // Powers/Player/Carnage/MaceHand.prototype
        {  11492u, "Emma Frost" },  // Powers/Player/EmmaFrost/BasicSpiritGainMissileEffect.prototype
        {  11493u, "Carnage" },  // Powers/Player/Carnage/ProtectionGain10PctSpender.prototype
        {  11494u, "Carnage" },  // Powers/Player/Carnage/MaceHandEffect.prototype
        {  11495u, "Magik" },  // Powers/Player/Magik/LimboSpitterSummonCombo.prototype
        {  11497u, "Captain America" },  // Powers/Player/CaptainAmerica/Traits/OffenseTrait.prototype
        {  11499u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Execute.prototype
        {  11500u, "Carnage" },  // Powers/Player/Carnage/Talents/SavageRebirthRezEffect.prototype
        {  11501u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/MinigunDamageBuffTalented.prototype
        {  11504u, "War Machine" },  // Powers/Player/WarMachine/PlasmaCannonHotspotEffect.prototype
        {  11508u, "Loki" },  // Powers/Player/Loki/AsgardianLightComboHeal.prototype
        {  11509u, "Wolverine" },  // Powers/Player/Wolverine/BerserkerBarrageComboSummon.prototype
        {  11511u, "Juggernaut" },  // Powers/Player/Juggernaut/PeoplesElbowEnd.prototype
        {  11512u, "Wolverine" },  // Powers/Player/Wolverine/Talents/Talent3RampageBuffs.prototype
        {  11513u, "Elektra" },  // Powers/Player/Elektra/Traits/DefenseTrait.prototype
        {  11514u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/UltimateCCImmunityCombo.prototype
        {  11516u, "Luke Cage" },  // Powers/Player/LukeCage/SweetChristmas.prototype
        {  11518u, "Wolverine" },  // Powers/Player/Wolverine/RawrBuff.prototype
        {  11522u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzyTributeGain.prototype
        {  11524u, "Black Bolt" },  // Powers/Player/BlackBolt/EnergyGain20PctCombo.prototype
        {  11525u, "Psylocke" },  // Powers/Player/Psylocke/ConeBlastBonusStun.prototype
        {  11526u, "Blade" },  // Powers/Player/Blade/StackableBleedMeleeArea.prototype
        {  11528u, "Deadpool" },  // Powers/Player/Deadpool/Rework/ServerLagVisualCombo.prototype
        {  11529u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/DarkPhoenixPassiveProcEffect.prototype
        {  11530u, "Doctor Strange" },  // Powers/Player/DoctorStrange/AstralLegionDamageBoost.prototype
        {  11531u, "Moon Knight" },  // Powers/Player/MoonKnight/SummonKhonshuStatueCombo.prototype
        {  11532u, "Loki" },  // Powers/Player/Loki/LightColumn.prototype
        {  11534u, "Hawkeye" },  // Powers/Player/Hawkeye/TurretBonusBasicArrow.prototype
        {  11535u, "Psylocke" },  // Powers/Player/Psylocke/KatanaPBAoEDecoyPower.prototype
        {  11538u, "Winter Soldier" },  // Powers/Player/WinterSoldier/ArmBlastCombo.prototype
        {  11542u, "Psylocke" },  // Powers/Player/Psylocke/Traits/OffenseTrait.prototype
        {  11547u, "Beast" },  // Powers/Player/Beast/GlueBombVuln.prototype
        {  11551u, "Carnage" },  // Powers/Player/Carnage/Talents/BladeWeaponsRanged.prototype
        {  11555u, "Wolverine" },  // Powers/Player/Wolverine/FlyingBleedEnd.prototype
        {  11560u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveDomino.prototype
        {  11561u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BlackBoltWhisper.prototype
        {  11563u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshusFavorGainProcEffect.prototype
        {  11564u, "Human Torch" },  // Powers/Player/HumanTorch/PassiveGetUpClose.prototype
        {  11565u, "Carnage" },  // Powers/Player/Carnage/Traits/DefenseTrait.prototype
        {  11568u, "War Machine" },  // Powers/Player/WarMachine/AutogunMissileEffect.prototype
        {  11570u, "Colossus" },  // Powers/Player/Colossus/MovementSpinComboSummon.prototype
        {  11572u, "Nova" },  // Powers/Player/Nova/PulsarImplosionRandomLocation.prototype
        {  11573u, "Angela" },  // Powers/Player/Angela/SwordPummelEnduranceGain.prototype
        {  11574u, "Doctor Strange" },  // Powers/Player/DoctorStrange/VaporsHotspotIntervalEffect.prototype
        {  11575u, "Iceman" },  // Powers/Player/Iceman/ChillAsCombo.prototype
        {  11576u, "Daredevil" },  // Powers/Player/Daredevil/Talents/NormalPointsBuffTalent.prototype
        {  11579u, "Storm" },  // Powers/Player/Storm/MaelstromSlowHotspotEffect.prototype
        {  11580u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/UltimateInvulnCombo.prototype
        {  11581u, "Iron Man" },  // Powers/Player/IronMan/RainOfMissilesVulnerability.prototype
        {  11582u, "Dr. Doom" },  // Powers/Player/DrDoom/AirStrike.prototype
        {  11583u, "Thor" },  // Powers/Player/Thor/Talents/StormcallerTalent.prototype
        {  11584u, "Gambit" },  // Powers/Player/Gambit/UltimateRogueDefaultAttackCombo4.prototype
        {  11586u, "Kitty Pryde" },  // Powers/Player/KittyPryde/UltimateComboLeft.prototype
        {  11593u, "Taskmaster" },  // Powers/Player/Taskmaster/StudentsBuffBlue.prototype
        {  11596u, "Hulk" },  // Powers/Player/Hulk/Rework/Meteor.prototype
        {  11598u, "Juggernaut" },  // Powers/Player/Juggernaut/HighlightFullMomentumSpenders.prototype
        {  11599u, "Loki" },  // Powers/Player/Loki/EternalDarknessHotspotEffect.prototype
        {  11602u, "Storm" },  // Powers/Player/Storm/ElementalStormHotspotEffect.prototype
        {  11603u, "X-23" },  // Powers/Player/X23/UltTriggerScent.prototype
        {  11606u, "Venom" },  // Powers/Player/Venom/SigFreakoutImplosionComboBossBig.prototype
        {  11612u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ArmyFromNothingIntervalEffectWiccanMoreVuln.prototype
        {  11614u, "Thor" },  // Powers/Player/Thor/Rework/Shockwave.prototype
        {  11615u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/TippyToeSummonCombo.prototype
        {  11618u, "Cable" },  // Powers/Player/Cable/FutureBombArsenal.prototype
        {  11619u, "Rogue" },  // Powers/Player/Rogue/DiveBombEndCraneStance.prototype
        {  11623u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/PBAoEKnockdownChargeCounterVersion.prototype
        {  11624u, "Black Bolt" },  // Powers/Player/BlackBolt/GapCloseBleedCombo.prototype
        {  11625u, "Cable" },  // Powers/Player/Cable/EnergyPulseMissileEffect.prototype
        {  11629u, "Captain America" },  // Powers/Player/CaptainAmerica/DeflectionProcEffect.prototype
        {  11630u, "Blade" },  // Powers/Player/Blade/HemoglycerinGauntletCCImmuneCombo.prototype
        {  11632u, "Psylocke" },  // Powers/Player/Psylocke/ConeBlastBonusDamageStun.prototype
        {  11634u, "Black Panther" },  // Powers/Player/BlackPanther/MineFieldActivationRanged.prototype
        {  11636u, "Magneto" },  // Powers/Player/Magneto/Talents/MaxDebrisBuff.prototype
        {  11637u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/NeverKnowWhatHitThem.prototype
        {  11638u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/MysticismDamagePulse.prototype
        {  11639u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/AutoSlapRecurringEffect.prototype
        {  11642u, "Angela" },  // Powers/Player/Angela/Talents/SwordBuffs.prototype
        {  11645u, "Winter Soldier" },  // Powers/Player/WinterSoldier/KnifeThrowMissileEffect.prototype
        {  11646u, "Blade" },  // Powers/Player/Blade/BloodlustHiddenPassive.prototype
        {  11647u, "Hulk" },  // Powers/Player/Hulk/Rework/DashCrashOverlap.prototype
        {  11650u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ElektraShadowStrikeDrop.prototype
        {  11652u, "Nick Fury" },  // Powers/Player/NickFury/StealthApproach.prototype
        {  11653u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/HerbieAttack.prototype
        {  11654u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Talents/SignatureRestoreTalent.prototype
        {  11655u, "Gambit" },  // Powers/Player/Gambit/TumbleHasteCombo.prototype
        {  11657u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TKTossJeanEffect.prototype
        {  11658u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BigBeamMissileEffect.prototype
        {  11660u, "Thor" },  // Powers/Player/Thor/Talents/HybridManTalent.prototype
        {  11661u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/SealOfOshtur.prototype
        {  11662u, "Gambit" },  // Powers/Player/Gambit/GrandSlam1stHit.prototype
        {  11664u, "Dr. Doom" },  // Powers/Player/DrDoom/FingerLasersPvPCooldownActiveLong.prototype
        {  11673u, "Angela" },  // Powers/Player/Angela/WhippingRibbons.prototype
        {  11675u, "Blade" },  // Powers/Player/Blade/StackableBleedSlow.prototype
        {  11676u, "Iron Man" },  // Powers/Player/IronMan/SonicShockwave.prototype
        {  11678u, "Gambit" },  // Powers/Player/Gambit/Traits/DefenseTrait.prototype
        {  11680u, "Taskmaster" },  // Powers/Player/Taskmaster/SteroidHotspotAudioCondition.prototype
        {  11682u, "Elektra" },  // Powers/Player/Elektra/TeleportDashStealthCombo.prototype
        {  11684u, "Nick Fury" },  // Powers/Player/NickFury/CallRedwingHotspotEffect.prototype
        {  11687u, "Black Cat" },  // Powers/Player/BlackCat/ClawPummel6.prototype
        {  11688u, "Blade" },  // Powers/Player/Blade/HemoglycerinGauntletExplosionOnDeath.prototype
        {  11689u, "Winter Soldier" },  // Powers/Player/WinterSoldier/RapidFireReload.prototype
        {  11691u, "Juggernaut" },  // Powers/Player/Juggernaut/BigChargeProcEffect.prototype
        {  11693u, "Thing" },  // Powers/Player/Thing/Traits/OffenseTrait.prototype
        {  11695u, "Black Panther" },  // Powers/Player/BlackPanther/PantherBombDamageBuff.prototype
        {  11696u, "Thing" },  // Powers/Player/Thing/Rework/CallSuzieHotspotKnockback.prototype
        {  11697u, "Nick Fury" },  // Powers/Player/NickFury/DangerCloseExplosion.prototype
        {  11698u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ForceDashStealthCombo.prototype
        {  11702u, "Psylocke" },  // Powers/Player/Psylocke/StealthMechanicHiddenPassive.prototype
        {  11704u, "Iceman" },  // Powers/Player/Iceman/UltimateHiddenPassive.prototype
        {  11705u, "Jean Grey" },  // Powers/Player/JeanGrey/PhoenixModeToggleSpecHybridSteroid.prototype
        {  11706u, "Iron Fist" },  // Powers/Player/IronFist/SnakeStanceBuff.prototype
        {  11712u, "Vision" },  // Powers/Player/Vision/EnhanceRobotBuffTauntBegin.prototype
        {  11714u, "Elektra" },  // Powers/Player/Elektra/BasicSaiEnergyGainCombo.prototype
        {  11715u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/MistyKnight/MistyKnightShot.prototype
        {  11716u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/BladeUnleashGlaiveMissileEffect.prototype
        {  11718u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/RhinoBigCharge.prototype
        {  11721u, "Wolverine" },  // Powers/Player/Wolverine/Talents/CantKeepMeDownRemoveHealthTransfer.prototype
        {  11722u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/MistyKnight/MistyKnightFollowupShot.prototype
        {  11723u, "Green Goblin" },  // Powers/Player/GreenGoblin/ExplosivePumpkinDoTCombo.prototype
        {  11724u, "Gambit" },  // Powers/Player/Gambit/Talents/Talent4JacksOrBetter.prototype
        {  11726u, "Punisher" },  // Powers/Player/Punisher/Rework/MinigunHotspotEffect.prototype
        {  11728u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaoticDebuffSlow.prototype
        {  11731u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/TaskmasterBoomerangShieldMissileEffect.prototype
        {  11732u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosBlastBuffFromImplosion.prototype
        {  11733u, "Doctor Strange" },  // Powers/Player/DoctorStrange/FangNuke.prototype
        {  11734u, "Psylocke" },  // Powers/Player/Psylocke/PsiKnife.prototype
        {  11736u, "Black Widow" },  // Powers/Player/BlackWidow/RapidShotMissileEffectTooltip.prototype
        {  11738u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MalekithDarkBeam.prototype
        {  11743u, "Iron Man" },  // Powers/Player/IronMan/Talents/UpgradeMissilePayloads.prototype
        {  11747u, "Luke Cage" },  // Powers/Player/LukeCage/Pummel7thAttack.prototype
        {  11748u, "Black Cat" },  // Powers/Player/BlackCat/NineLivesDisableHealthMinHiddenPassive.prototype
        {  11749u, "Black Panther" },  // Powers/Player/BlackPanther/DashStackRechargeCombo.prototype
        {  11751u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/AlterRealityHealProc.prototype
        {  11753u, "Thor" },  // Powers/Player/Thor/Rework/RagnarokForwardOuterDamageCombo.prototype
        {  11754u, "Ghost Rider" },  // Powers/Player/GhostRider/ChainsHealProc.prototype
        {  11757u, "Magneto" },  // Powers/Player/Magneto/MagneticSphere.prototype
        {  11758u, "Dr. Doom" },  // Powers/Player/DrDoom/MagicLanceMissileEffectTooltip.prototype
        {  11759u, "Magneto" },  // Powers/Player/Magneto/Traits/DefenseTrait.prototype
        {  11761u, "Blade" },  // Powers/Player/Blade/RapidFire.prototype
        {  11762u, "War Machine" },  // Powers/Player/WarMachine/ThermiteRoundsCondition.prototype
        {  11763u, "Gambit" },  // Powers/Player/Gambit/SleightOfHandDodgeProcEffect.prototype
        {  11764u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent4AssaultAndBattery.prototype
        {  11765u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/BasicRifleMissileEffectTalented.prototype
        {  11767u, "Captain America" },  // Powers/Player/CaptainAmerica/UltimateStartImmobilize.prototype
        {  11768u, "Magneto" },  // Powers/Player/Magneto/BoomerangMetal.prototype
        {  11769u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent1MentalBuff.prototype
        {  11770u, "Beast" },  // Powers/Player/Beast/MeleeBasic.prototype
        {  11772u, "Cable" },  // Powers/Player/Cable/FutureBombMentalExplosionMentalKeyword.prototype
        {  11773u, "Venom" },  // Powers/Player/Venom/SigFreakoutDeflectBuff.prototype
        {  11774u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TelepathicIllusionJean.prototype
        {  11778u, "Nick Fury" },  // Powers/Player/NickFury/SniperShotCooldownEndDelayedCombo.prototype
        {  11780u, "Blade" },  // Powers/Player/Blade/UVArc3rdHit.prototype
        {  11783u, "Deadpool" },  // Powers/Player/Deadpool/SwordFuryEffect.prototype
        {  11789u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/ElektraMarkForDeathOnDeathChargeGain.prototype
        {  11792u, "Angela" },  // Powers/Player/Angela/DFAEndAndReset.prototype
        {  11796u, "Blade" },  // Powers/Player/Blade/JustStayDownHotspotEffect.prototype
        {  11799u, "Doctor Strange" },  // Powers/Player/DoctorStrange/MysticEnergyFullProcEffect.prototype
        {  11800u, "Cyclops" },  // Powers/Player/Cyclops/CallEmmaDiamondHeelDropKnockdown.prototype
        {  11806u, "Luke Cage" },  // Powers/Player/LukeCage/Talents/JessicaJones.prototype
        {  11808u, "Storm" },  // Powers/Player/Storm/Tornado.prototype
        {  11809u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DarkPhoenixMaelstromEffect.prototype
        {  11810u, "Nick Fury" },  // Powers/Player/NickFury/EyesEverywhereSniperShots.prototype
        {  11812u, "Rogue" },  // Powers/Player/Rogue/Talents/RecallOverloadPhysical.prototype
        {  11816u, "Iron Fist" },  // Powers/Player/IronFist/TigerClawSingleStanceBuff.prototype
        {  11817u, "Dr. Doom" },  // Powers/Player/DrDoom/DoomsDayLightningArc.prototype
        {  11818u, "Cable" },  // Powers/Player/Cable/FutureBombMentalExplosionGunKeyword.prototype
        {  11819u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallIceman.prototype
        {  11822u, "Winter Soldier" },  // Powers/Player/WinterSoldier/TeamStealth.prototype
        {  11827u, "Punisher" },  // Powers/Player/Punisher/Talents/HollowPointRounds.prototype
        {  11833u, "Magneto" },  // Powers/Player/Magneto/Talents/BoomerangScrapCooldownReduction.prototype
        {  11834u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ForceDashStealthVersion.prototype
        {  11835u, "Winter Soldier" },  // Powers/Player/WinterSoldier/ArmBlast.prototype
        {  11837u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Vapors.prototype
        {  11838u, "Cyclops" },  // Powers/Player/Cyclops/CarryTheMomentumSpiritReduceEffe.prototype
        {  11840u, "Green Goblin" },  // Powers/Player/GreenGoblin/Talents/SignatureResistanceTalent.prototype
        {  11842u, "Vision" },  // Powers/Player/Vision/GroundSmashDFAWeakenCombo.prototype
        {  11848u, "Iron Fist" },  // Powers/Player/IronFist/IronFistPunchDragonInvulnerability.prototype
        {  11849u, "Luke Cage" },  // Powers/Player/LukeCage/DefensiveLeaderTaunt.prototype
        {  11851u, "Angela" },  // Powers/Player/Angela/Talents/AutoDisablingRibbons.prototype
        {  11852u, "Storm" },  // Powers/Player/Storm/SiroccoLungeStunEffect.prototype
        {  11853u, "Psylocke" },  // Powers/Player/Psylocke/ConeBlast.prototype
        {  11854u, "Carnage" },  // Powers/Player/Carnage/ClawPummelHealthOnHitCombo.prototype
        {  11856u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/BouncingBubble.prototype
        {  11859u, "Magik" },  // Powers/Player/Magik/Talents/Talent3NastirthIntoBFLD.prototype
        {  11860u, "Green Goblin" },  // Powers/Player/GreenGoblin/GoblinSerumReviveInvulnerabilityCombo.prototype
        {  11864u, "Storm" },  // Powers/Player/Storm/ChargedStrikeChargedVFXEffect.prototype
        {  11865u, "Moon Knight" },  // Powers/Player/MoonKnight/Traits/MechanicTrait.prototype
        {  11869u, "X-23" },  // Powers/Player/X23/TripleKick3rdHit.prototype
        {  11875u, "Hawkeye" },  // Powers/Player/Hawkeye/Traits/DefenseTrait.prototype
        {  11876u, "Cable" },  // Powers/Player/Cable/Talents/TechnoOrganicInterface.prototype
        {  11879u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallBeast.prototype
        {  11880u, "Vision" },  // Powers/Player/Vision/Talents/Talent2PhaseModeBuff.prototype
        {  11882u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveHood.prototype
        {  11884u, "Punisher" },  // Powers/Player/Punisher/Rework/PassiveToughSelfRezCDDisplay.prototype
        {  11885u, "Thor" },  // Powers/Player/Thor/StormHammerSummon.prototype
        {  11889u, "Black Panther" },  // Powers/Player/BlackPanther/DisengagingShot.prototype
        {  11890u, "Rogue" },  // Powers/Player/Rogue/UltimateDashSlash4.prototype
        {  11891u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent3BetterMovementBarrierRestore.prototype
        {  11892u, "Taskmaster" },  // Powers/Player/Taskmaster/WidowsBiteChainEffect.prototype
        {  11893u, "Carnage" },  // Powers/Player/Carnage/Traits/SymbioteArmorHiddenPassive.prototype
        {  11895u, "Gambit" },  // Powers/Player/Gambit/UltimateCardThrow4.prototype
        {  11896u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/StretchyBrainActivateGiantGun.prototype
        {  11897u, "Hawkeye" },  // Powers/Player/Hawkeye/TenArrowSpeedLoader.prototype
        {  11902u, "Loki" },  // Powers/Player/Loki/MagicCrushRingDamageCombo.prototype
        {  11903u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexSphereOrbVisual1.prototype
        {  11904u, "Juggernaut" },  // Powers/Player/Juggernaut/StrideDamageSummonCombo.prototype
        {  11908u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/DeathFromAboveEnd.prototype
        {  11909u, "Rogue" },  // Powers/Player/Rogue/RecallOverloadEndExplosionPhysical.prototype
        {  11912u, "Human Torch" },  // Powers/Player/HumanTorch/FlameCycloneMovementDash.prototype
        {  11913u, "War Machine" },  // Powers/Player/WarMachine/HeatGainTeslaField.prototype
        {  11919u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosBlastBuffFromChaosHex.prototype
        {  11921u, "X-23" },  // Powers/Player/X23/ExecuteBleedFilterPower.prototype
        {  11923u, "Black Bolt" },  // Powers/Player/BlackBolt/DeathFromAboveEndVuln.prototype
        {  11926u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/TumbleAcrobaticAttack.prototype
        {  11928u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Talents/Talent1FlourishPowerCooldownReset.prototype
        {  11929u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/PainfulForce.prototype
        {  11931u, "Ultron" },  // Powers/Player/Ultron/SummonBladeDroneProcEffect.prototype
        {  11932u, "Hulk" },  // Powers/Player/Hulk/Rework/Talent2DeflectBonusRevive.prototype
        {  11935u, "Luke Cage" },  // Powers/Player/LukeCage/UltimateBoulderBeatdownFinalHit.prototype
        {  11939u, "Juggernaut" },  // Powers/Player/Juggernaut/SundayPunchFullSpender.prototype
        {  11940u, "Angela" },  // Powers/Player/Angela/SigNoMatchTwoStackVulnerability.prototype
        {  11941u, "Loki" },  // Powers/Player/Loki/DarkBoltMissileEffect.prototype
        {  11943u, "Blade" },  // Powers/Player/Blade/UVArc.prototype
        {  11944u, "Black Cat" },  // Powers/Player/BlackCat/DeathFromAboveEnd.prototype
        {  11945u, "Ultron" },  // Powers/Player/Ultron/UltimateInvulnConditionPower.prototype
        {  11946u, "Storm" },  // Powers/Player/Storm/TyphoonRainHotspotEffect.prototype
        {  11947u, "X-23" },  // Powers/Player/X23/SigBladeDanceRecharge.prototype
        {  11951u, "Nova" },  // Powers/Player/Nova/ChargedDashNoWindup.prototype
        {  11956u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/BouncyWhirlwind.prototype
        {  11958u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CosmicRiftHotspotEffect.prototype
        {  11961u, "Colossus" },  // Powers/Player/Colossus/Traits/ArmorRegenInCombatPause.prototype
        {  11962u, "Black Panther" },  // Powers/Player/BlackPanther/MineFieldExplosionRanged.prototype
        {  11963u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveDominoProcEffectCrit.prototype
        {  11968u, "Cable" },  // Powers/Player/Cable/Talents/MindBarrierLayer.prototype
        {  11970u, "War Machine" },  // Powers/Player/WarMachine/DeathFromAboveComboEffect.prototype
        {  11972u, "Green Goblin" },  // Powers/Player/GreenGoblin/HallucinogenicPumpkinDoT.prototype
        {  11974u, "Venom" },  // Powers/Player/Venom/DefensePassiveDamageNegationProc.prototype
        {  11979u, "Beast" },  // Powers/Player/Beast/PummelEndandReset.prototype
        {  11981u, "Cable" },  // Powers/Player/Cable/EnergyPulseMissileEffectPlus.prototype
        {  11983u, "Iron Fist" },  // Powers/Player/IronFist/Pummel3rdAttack.prototype
        {  11984u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HydeDirectedShockwaveMissileEff.prototype
        {  11985u, "Iceman" },  // Powers/Player/Iceman/ChillAsHotspot.prototype
        {  11987u, "War Machine" },  // Powers/Player/WarMachine/AutogunPassive.prototype
        {  11995u, "Wolverine" },  // Powers/Player/Wolverine/Ultimate.prototype
        {  11996u, "She-Hulk" },  // Powers/Player/SheHulk/MoveToStrikeEnd.prototype
        {  11999u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondStrikeComboEnduranceGain.prototype
        {  12005u, "Deadpool" },  // Powers/Player/Deadpool/Rework/GodModeAutoRevive.prototype
        {  12008u, "Gambit" },  // Powers/Player/Gambit/RaininPainSlow.prototype
        {  12014u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/UltimateMissileLauncher.prototype
        {  12015u, "Thing" },  // Powers/Player/Thing/Traits/ClobberinTimeGainOnHit.prototype
        {  12018u, "Winter Soldier" },  // Powers/Player/WinterSoldier/BulletSpray.prototype
        {  12019u, "Deadpool" },  // Powers/Player/Deadpool/Talents/MultiplayerTalent.prototype
        {  12021u, "Luke Cage" },  // Powers/Player/LukeCage/BusinessIsGoodHotspotEffect.prototype
        {  12023u, "Nova" },  // Powers/Player/Nova/FuriousLungeMoveSpeedBuff2.prototype
        {  12024u, "Iron Man" },  // Powers/Player/IronMan/UltimateSuitBasicPunch3.prototype
        {  12026u, "Elektra" },  // Powers/Player/Elektra/TripleChainKnifeRopeMasteryCDR.prototype
        {  12038u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallBeastSummonCombo.prototype
        {  12039u, "Taskmaster" },  // Powers/Player/Taskmaster/BasicShotTwoHiddenPassive.prototype
        {  12040u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/WarpTurretDamageReductionCombo.prototype
        {  12041u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/SignatureMicroNullifier.prototype
        {  12044u, "Vision" },  // Powers/Player/Vision/SolarConeEnergyDamageBonus.prototype
        {  12048u, "Iron Fist" },  // Powers/Player/IronFist/SevenSidedStrikeHit.prototype
        {  12049u, "Colossus" },  // Powers/Player/Colossus/MetalRegeneration.prototype
        {  12050u, "Gambit" },  // Powers/Player/Gambit/RoyalFlush.prototype
        {  12052u, "Iceman" },  // Powers/Player/Iceman/SummonStanceMeleeProc.prototype
        {  12057u, "Thor" },  // Powers/Player/Thor/Rework/LightningStrikeNoOF.prototype
        {  12060u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/KaeciliusHealChannelErruptionsRandom.prototype
        {  12062u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SurturSwordAttackHotspotEffect.prototype
        {  12066u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/ArcTurretWeaken.prototype
        {  12070u, "Green Goblin" },  // Powers/Player/GreenGoblin/PBAoESuperSpinReflectAreaSummon.prototype
        {  12074u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/StretchyDash.prototype
        {  12075u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallMagneto.prototype
        {  12076u, "Daredevil" },  // Powers/Player/Daredevil/ComboPointStackBuff.prototype
        {  12077u, "Beast" },  // Powers/Player/Beast/UltimateSawsForDays.prototype
        {  12078u, "Thor" },  // Powers/Player/Thor/OdinforceHiddenPassive.prototype
        {  12080u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveDaredevilComboPointGainPoint.prototype
        {  12081u, "Taskmaster" },  // Powers/Player/Taskmaster/StudentsBuffYellow.prototype
        {  12082u, "Nova" },  // Powers/Player/Nova/PulsarKillCombo.prototype
        {  12089u, "Magneto" },  // Powers/Player/Magneto/Talents/AutoDebrisShield.prototype
        {  12090u, "Human Torch" },  // Powers/Player/HumanTorch/FallbackBlast.prototype
        {  12092u, "Black Widow" },  // Powers/Player/BlackWidow/TwilightInitiative.prototype
        {  12100u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ScarletWitchShadowBoltMissileEff.prototype
        {  12104u, "Daredevil" },  // Powers/Player/Daredevil/DodgePassivePingEffect.prototype
        {  12105u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/IronMaidenAsCombo.prototype
        {  12108u, "Deadpool" },  // Powers/Player/Deadpool/Talents/DevilHammerExplosionTalent.prototype
        {  12114u, "Rogue" },  // Powers/Player/Rogue/DrainLifeDoT.prototype
        {  12121u, "Gambit" },  // Powers/Player/Gambit/EnhancedCost50.prototype
        {  12122u, "Doctor Strange" },  // Powers/Player/DoctorStrange/EyeOfAgamottoBeamAttack.prototype
        {  12126u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/BasicTripleSquirrelKill.prototype
        {  12127u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PsylockeLungeEffect.prototype
        {  12128u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/AutoShield.prototype
        {  12129u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Traits/LockheedEnergyRegen.prototype
        {  12132u, "Elektra" },  // Powers/Player/Elektra/KillCommand.prototype
        {  12137u, "Thing" },  // Powers/Player/Thing/InterceptingChargeEffect.prototype
        {  12138u, "Black Panther" },  // Powers/Player/BlackPanther/BasicDaggerThrowEffect.prototype
        {  12141u, "Iceman" },  // Powers/Player/Iceman/DeepFreezeFilterPowerHotspotBeam.prototype
        {  12144u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/JuggernautImInvulnerableRemoveDecays.prototype
        {  12145u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ReconstructionAutoTimer.prototype
        {  12146u, "Iceman" },  // Powers/Player/Iceman/IceGolemSnowballMissileEffect.prototype
        {  12149u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedEnergyPauseHiddenPassive.prototype
        {  12150u, "Dr. Doom" },  // Powers/Player/DrDoom/Talents/Talent2DoomBotsBonus.prototype
        {  12151u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/InvisibilityStackingDamageBuff.prototype
        {  12157u, "Hawkeye" },  // Powers/Player/Hawkeye/ShriekingArrowExplosionBurn.prototype
        {  12162u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/KneelBeforeMeComboExplosion.prototype
        {  12163u, "Blade" },  // Powers/Player/Blade/SignatureReduceCooldown.prototype
        {  12165u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BrimstoneBlitzAoEHit.prototype
        {  12168u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/Minigun.prototype
        {  12170u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Talents/Talent4BionicsDmgBuff.prototype
        {  12174u, "Iron Man" },  // Powers/Player/IronMan/UniBeam.prototype
        {  12176u, "Cyclops" },  // Powers/Player/Cyclops/CallEmmaKneelBeforeMe.prototype
        {  12177u, "Thing" },  // Powers/Player/Thing/Talents/Talent5GroundSmashBuff.prototype
        {  12179u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotBlockadeHiddenPassiveDisabler.prototype
        {  12181u, "Magik" },  // Powers/Player/Magik/BFLDBackhandRight.prototype
        {  12182u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosBlastBuffFromRavenousBinding.prototype
        {  12183u, "Cyclops" },  // Powers/Player/Cyclops/Rework/DisengagingShotMissileEffect.prototype
        {  12185u, "Beast" },  // Powers/Player/Beast/ShieldGadget.prototype
        {  12186u, "Thing" },  // Powers/Player/Thing/KnockdownChargeProcEffect.prototype
        {  12190u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/JuggernautImInvulnerableMomentumDecay.prototype
        {  12193u, "Cable" },  // Powers/Player/Cable/ParticleAcceleratorFirstExplosion.prototype
        {  12195u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/GrootDeathFromAboveCombo.prototype
        {  12196u, "Black Widow" },  // Powers/Player/BlackWidow/UltimateAgentBBulletSpray.prototype
        {  12200u, "Thing" },  // Powers/Player/Thing/Talents/Talent2SignatureCooldownReduction.prototype
        {  12201u, "Deadpool" },  // Powers/Player/Deadpool/Rework/SuperiorHealingFactorLowHealthProc.prototype
        {  12202u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LokiIllusionMeleeAttack4.prototype
        {  12209u, "Juggernaut" },  // Powers/Player/Juggernaut/ShockwaveEnhancedMissileEffect.prototype
        {  12212u, "Dr. Doom" },  // Powers/Player/DrDoom/ElectricBlastPvPCooldownActiveLong.prototype
        {  12214u, "Dr. Doom" },  // Powers/Player/DrDoom/AoEDebuffCowerCombo.prototype
        {  12215u, "Punisher" },  // Powers/Player/Punisher/Rework/MagnumMissileEffect.prototype
        {  12216u, "Luke Cage" },  // Powers/Player/LukeCage/SummonIronFist.prototype
        {  12218u, "Daredevil" },  // Powers/Player/Daredevil/Update/NunchuckBulldoze.prototype
        {  12221u, "Luke Cage" },  // Powers/Player/LukeCage/Talents/ComboPointsIncreaseMax.prototype
        {  12224u, "Loki" },  // Powers/Player/Loki/FrostNova.prototype
        {  12225u, "Ant-Man" },  // Powers/Player/AntMan/MultiStrikeBouncingBulletTalentPower.prototype
        {  12226u, "Nick Fury" },  // Powers/Player/NickFury/RocketLauncher.prototype
        {  12228u, "Moon Knight" },  // Powers/Player/MoonKnight/TributeGainEffectCrit.prototype
        {  12230u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/DefenseDeflectTalentHealCombo.prototype
        {  12232u, "Hulk" },  // Powers/Player/Hulk/Rework/ShockwavePBAoESmall.prototype
        {  12234u, "X-23" },  // Powers/Player/X23/WrathOnBleedTick.prototype
        {  12235u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateCallIceman.prototype
        {  12238u, "Vision" },  // Powers/Player/Vision/AtomicFootDiveHotspotSummon.prototype
        {  12244u, "Taskmaster" },  // Powers/Player/Taskmaster/SwordStrike.prototype
        {  12252u, "Carnage" },  // Powers/Player/Carnage/TransfusionPBAoE.prototype
        {  12258u, "Black Cat" },  // Powers/Player/BlackCat/Tumble.prototype
        {  12259u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseToggle.prototype
        {  12260u, "Blade" },  // Powers/Player/Blade/HemoglycerinGauntlet4thAttack.prototype
        {  12261u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ImplosionEffect.prototype
        {  12264u, "Ghost Rider" },  // Powers/Player/GhostRider/PenanceStareFearCombo.prototype
        {  12265u, "Wolverine" },  // Powers/Player/Wolverine/BloodySteroid.prototype
        {  12266u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/PsychicSpear.prototype
        {  12268u, "Storm" },  // Powers/Player/Storm/TyphoonHiddenPassive.prototype
        {  12269u, "Green Goblin" },  // Powers/Player/GreenGoblin/GoblinLaser.prototype
        {  12272u, "Loki" },  // Powers/Player/Loki/MagicCrushKeywordConditionCombo.prototype
        {  12274u, "Deadpool" },  // Powers/Player/Deadpool/BulletSprayHotspotEffect.prototype
        {  12276u, "Black Widow" },  // Powers/Player/BlackWidow/RollingGrenadesDoT.prototype
        {  12281u, "Venom" },  // Powers/Player/Venom/SigFreakoutWebAttachVisualComboBig.prototype
        {  12282u, "Vision" },  // Powers/Player/Vision/ControlRobot.prototype
        {  12290u, "Cyclops" },  // Powers/Player/Cyclops/Rework/DisengagingShot.prototype
        {  12291u, "Black Panther" },  // Powers/Player/BlackPanther/Talents/DoraOffensiveBonusTalent.prototype
        {  12292u, "Deadpool" },  // Powers/Player/Deadpool/Rework/InvulnerableCombo.prototype
        {  12293u, "Venom" },  // Powers/Player/Venom/WrithingTendrilsDamageBonusCombo.prototype
        {  12295u, "Cable" },  // Powers/Player/Cable/PulseBolt.prototype
        {  12296u, "Storm" },  // Powers/Player/Storm/Traits/OffenseTrait.prototype
        {  12297u, "Black Widow" },  // Powers/Player/BlackWidow/DodgeMartialArtsPassiveComboEffe.prototype
        {  12299u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DarkPhoenixEnduranceRestore.prototype
        {  12300u, "Iron Fist" },  // Powers/Player/IronFist/Talents/Talent5SingleStance.prototype
        {  12302u, "Moon Knight" },  // Powers/Player/MoonKnight/ExplosiveCrescentDartSingleHitVuln.prototype
        {  12303u, "Nightcrawler" },  // Powers/Player/Nightcrawler/ExecuteCombo.prototype
        {  12305u, "Storm" },  // Powers/Player/Storm/Fog.prototype
        {  12306u, "Ant-Man" },  // Powers/Player/AntMan/RapidShrinkStrikeConditionPower.prototype
        {  12307u, "Beast" },  // Powers/Player/Beast/Talents/Talent1BrainsCDR.prototype
        {  12310u, "Iceman" },  // Powers/Player/Iceman/SpikePunchIceWallDeathAnimCancel.prototype
        {  12311u, "Deadpool" },  // Powers/Player/Deadpool/BasicShotMissileEffect.prototype
        {  12313u, "Cyclops" },  // Powers/Player/Cyclops/ConeBeamMeleeBuff.prototype
        {  12315u, "Black Panther" },  // Powers/Player/BlackPanther/DisengagingShotMissileEffect.prototype
        {  12319u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHoleSummonLocusCombo.prototype
        {  12322u, "X-23" },  // Powers/Player/X23/SigBladeDanceComboSummon.prototype
        {  12323u, "Nick Fury" },  // Powers/Player/NickFury/HeadsDown.prototype
        {  12325u, "Loki" },  // Powers/Player/Loki/UltimateFrostNovaEffect.prototype
        {  12326u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent4CallInBuffsMagik.prototype
        {  12329u, "Human Torch" },  // Powers/Player/HumanTorch/FlameTornadoCycloneHotspotEffect.prototype
        {  12331u, "Cyclops" },  // Powers/Player/Cyclops/Rework/ChanneledBeamUpgraded.prototype
        {  12333u, "Punisher" },  // Powers/Player/Punisher/Rework/HighlightReloadProcEffect.prototype
        {  12336u, "Ant-Man" },  // Powers/Player/AntMan/BioElectricBlast.prototype
        {  12348u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexSphereInstantKill.prototype
        {  12349u, "Storm" },  // Powers/Player/Storm/Talents/FreezingTempest.prototype
        {  12350u, "Venom" },  // Powers/Player/Venom/IchorVisualPassive.prototype
        {  12352u, "Angela" },  // Powers/Player/Angela/Talents/WhippingRibbonsYankTalent.prototype
        {  12356u, "Venom" },  // Powers/Player/Venom/IchorSpike.prototype
        {  12357u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PsylockeLungeDamageShield.prototype
        {  12358u, "Rogue" },  // Powers/Player/Rogue/DiveBombVulnerabilityCombo.prototype
        {  12359u, "Magneto" },  // Powers/Player/Magneto/AllIn.prototype
        {  12361u, "Thor" },  // Powers/Player/Thor/Rework/UltimateOdinforceRegenCombo.prototype
        {  12362u, "Beast" },  // Powers/Player/Beast/BeastBamfBros.prototype
        {  12364u, "Iron Fist" },  // Powers/Player/IronFist/StanceChangeChiRestore.prototype
        {  12365u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedFireball.prototype
        {  12366u, "Hulk" },  // Powers/Player/Hulk/Rework/DashCrashEndCombo.prototype
        {  12369u, "Rogue" },  // Powers/Player/Rogue/StolenPowerLibrarySlot5.prototype
        {  12371u, "Deadpool" },  // Powers/Player/Deadpool/Rework/SuperiorHealingFactorHealthOnKillProc.prototype
        {  12372u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/KronanArcanistSummonHotspotEffect.prototype
        {  12373u, "Captain America" },  // Powers/Player/CaptainAmerica/Talents/SerumShieldThrows.prototype
        {  12374u, "Human Torch" },  // Powers/Player/HumanTorch/ChanneledEnergyBeamFireballCombo.prototype
        {  12375u, "Iron Fist" },  // Powers/Player/IronFist/Pummel5thAttack.prototype
        {  12377u, "Blade" },  // Powers/Player/Blade/SwordDashComboEffect.prototype
        {  12383u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/RangedSplashShot.prototype
        {  12386u, "Iceman" },  // Powers/Player/Iceman/Talents/ChilledDoTProcEffect.prototype
        {  12387u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GrootWakeImplosion.prototype
        {  12390u, "Taskmaster" },  // Powers/Player/Taskmaster/SwordSliceMissileEffect.prototype
        {  12391u, "Punisher" },  // Powers/Player/Punisher/Rework/SMG.prototype
        {  12393u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BladeBloodlust.prototype
        {  12395u, "Cable" },  // Powers/Player/Cable/TechnoOrganicVirusHealing.prototype
        {  12396u, "War Machine" },  // Powers/Player/WarMachine/FakeMissiles.prototype
        {  12397u, "Beast" },  // Powers/Player/Beast/UltimateSawBladeSummon.prototype
        {  12401u, "Daredevil" },  // Powers/Player/Daredevil/Update/NunchuckAttackStackingCastSpeedBuff.prototype
        {  12403u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/TKTossTripleThrow.prototype
        {  12406u, "Ant-Man" },  // Powers/Player/AntMan/FlyingAntSwarmDamageHotspotEffect.prototype
        {  12409u, "Iron Fist" },  // Powers/Player/IronFist/PummelTigerStanceCombo.prototype
        {  12410u, "Storm" },  // Powers/Player/Storm/TyphoonPullTowardsProcEffect.prototype
        {  12411u, "Ghost Rider" },  // Powers/Player/GhostRider/Talents/Talent5PenanceStare.prototype
        {  12415u, "Black Panther" },  // Powers/Player/BlackPanther/DoublePunchBleedFilterPower.prototype
        {  12416u, "Black Cat" },  // Powers/Player/BlackCat/NineLivesRegenProc.prototype
        {  12417u, "Juggernaut" },  // Powers/Player/Juggernaut/TriplePunch3rdHit.prototype
        {  12418u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/DarkHexDamageEffect.prototype
        {  12419u, "Taskmaster" },  // Powers/Player/Taskmaster/UltimateCooldownStart.prototype
        {  12421u, "Magik" },  // Powers/Player/Magik/BounceStrikeLonger.prototype
        {  12426u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/RangedSquirrelAoESlowEffect.prototype
        {  12430u, "Hulk" },  // Powers/Player/Hulk/HurlMissileEffect.prototype
        {  12432u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SilverSurferChanneledBeamStackCounter.prototype
        {  12435u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LimboDemonBossTeleportHotspotCombo.prototype
        {  12436u, "Angela" },  // Powers/Player/Angela/DFAFinalHitTimer.prototype
        {  12438u, "Luke Cage" },  // Powers/Player/LukeCage/UltimateInvulnCombo.prototype
        {  12439u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/PsychicHammerPhoenix.prototype
        {  12440u, "Human Torch" },  // Powers/Player/HumanTorch/PassiveGetUpCloseProcFilterPower.prototype
        {  12442u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzyExecuteComboRanged.prototype
        {  12443u, "Cyclops" },  // Powers/Player/Cyclops/Rework/SignatureBeamLong.prototype
        {  12446u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GrimReaperEnergyBlast.prototype
        {  12448u, "Cyclops" },  // Powers/Player/Cyclops/ChanneledEnergyBeamEffect.prototype
        {  12449u, "Emma Frost" },  // Powers/Player/EmmaFrost/ControlMobHealCombo.prototype
        {  12451u, "Silver Surfer" },  // Powers/Player/SilverSurfer/TeleportDash.prototype
        {  12453u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/MysticismRestoration.prototype
        {  12454u, "Black Bolt" },  // Powers/Player/BlackBolt/HealthRegenBuffProcEffect.prototype
        {  12456u, "Taskmaster" },  // Powers/Player/Taskmaster/Cocoon.prototype
        {  12457u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ChargedPBAoEKnockdown.prototype
        {  12462u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateIcemanProcSummon.prototype
        {  12466u, "Doctor Strange" },  // Powers/Player/DoctorStrange/DemonsOfDenakTransfer2.prototype
        {  12467u, "Elektra" },  // Powers/Player/Elektra/Talents/CooldownResetOnKillMark.prototype
        {  12469u, "Elektra" },  // Powers/Player/Elektra/SilentScream.prototype
        {  12472u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/StormHailstormLightningTempestBoltEffect.prototype
        {  12474u, "Beast" },  // Powers/Player/Beast/UltimateWreckingBallSummon.prototype
        {  12479u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HumanTorchNovaBurstStackingEffec.prototype
        {  12481u, "Human Torch" },  // Powers/Player/HumanTorch/PassiveGetUpCloseHotspotProcEffe.prototype
        {  12482u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/SignatureMicroNullifierInstantKill.prototype
        {  12484u, "Vision" },  // Powers/Player/Vision/ControlRobotAllianceOverride.prototype
        {  12489u, "Ultron" },  // Powers/Player/Ultron/PrimePetSteroid.prototype
        {  12490u, "Elektra" },  // Powers/Player/Elektra/ThrowShurikenExtraShuriken.prototype
        {  12492u, "Ant-Man" },  // Powers/Player/AntMan/FireAntAttack.prototype
        {  12494u, "Silver Surfer" },  // Powers/Player/SilverSurfer/DeconstructionComboBuff.prototype
        {  12495u, "She-Hulk" },  // Powers/Player/SheHulk/ConvictionBuffCombo.prototype
        {  12498u, "Colossus" },  // Powers/Player/Colossus/Punch.prototype
        {  12499u, "Captain America" },  // Powers/Player/CaptainAmerica/BrutalStrikeSerumGain.prototype
        {  12500u, "Captain America" },  // Powers/Player/CaptainAmerica/FirstStrike.prototype
        {  12503u, "Loki" },  // Powers/Player/Loki/MainSpecRangedBuffFire.prototype
        {  12507u, "Loki" },  // Powers/Player/Loki/UltimateIceOrbMissileEffect.prototype
        {  12508u, "Moon Knight" },  // Powers/Player/MoonKnight/ExplosiveCrescentDartExplosion.prototype
        {  12510u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/TeamStealthCombo.prototype
        {  12512u, "Vision" },  // Powers/Player/Vision/BigPunchHiddenPassive.prototype
        {  12515u, "Thor" },  // Powers/Player/Thor/Rework/LightningStrikeOF.prototype
        {  12517u, "Black Bolt" },  // Powers/Player/BlackBolt/AuraHotspotEffect.prototype
        {  12520u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveRocketRaccoonTeamUpBuffProcEffect.prototype
        {  12521u, "Magik" },  // Powers/Player/Magik/SoulCaptureSpawn.prototype
        {  12524u, "Silver Surfer" },  // Powers/Player/SilverSurfer/TimeWarpInvulnerability.prototype
        {  12532u, "Silver Surfer" },  // Powers/Player/SilverSurfer/TeleportDashComboEffect.prototype
        {  12534u, "Storm" },  // Powers/Player/Storm/Talents/ColdDamageSpecSlowProcEffect.prototype
        {  12536u, "Rogue" },  // Powers/Player/Rogue/DrainPunchRestoration.prototype
        {  12538u, "Winter Soldier" },  // Powers/Player/WinterSoldier/ArmBlastOuterDamageCombo.prototype
        {  12540u, "Nick Fury" },  // Powers/Player/NickFury/Traits/MechanicTraitAmmo.prototype
        {  12545u, "Magik" },  // Powers/Player/Magik/SoulShockwaveMissileEffectLarge.prototype
        {  12546u, "Black Bolt" },  // Powers/Player/BlackBolt/RangeDamageBuffConditionCombo.prototype
        {  12548u, "Black Panther" },  // Powers/Player/BlackPanther/EnergyTrapSummon.prototype
        {  12553u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SheHulkLawyerUpRefreshProc.prototype
        {  12556u, "Deadpool" },  // Powers/Player/Deadpool/SwordFuryComboEffect.prototype
        {  12568u, "Carnage" },  // Powers/Player/Carnage/MegaClawHiddenPassive.prototype
        {  12572u, "Wolverine" },  // Powers/Player/Wolverine/Impale.prototype
        {  12576u, "Iceman" },  // Powers/Player/Iceman/Traits/FrostArmorDamageAbsorbStopperEnd.prototype
        {  12579u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/KineticBoltPhoenix.prototype
        {  12580u, "Beast" },  // Powers/Player/Beast/Talents/Talent4BrainsHealthBuffProc.prototype
        {  12582u, "Hulk" },  // Powers/Player/Hulk/Rework/Tantrum3rdAttack.prototype
        {  12583u, "Nova" },  // Powers/Player/Nova/PulsarImplosionEffect.prototype
        {  12584u, "Taskmaster" },  // Powers/Player/Taskmaster/PoisonGasBomb.prototype
        {  12591u, "Iceman" },  // Powers/Player/Iceman/IcicleRangedMissileEffect.prototype
        {  12593u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateSummonXmen.prototype
        {  12596u, "Dr. Doom" },  // Powers/Player/DrDoom/AirStrikeExplosion.prototype
        {  12600u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DamageMaelstromPhoenixHotspotEffect.prototype
        {  12603u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/TimeDilatorHotspotEffect.prototype
        {  12604u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/CyclopsBouncingBeam.prototype
        {  12607u, "Taskmaster" },  // Powers/Player/Taskmaster/UltimateSpidermanAmazingSmashComboExplosion.prototype
        {  12615u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GrootRideRocketHotspotEffect.prototype
        {  12616u, "Wolverine" },  // Powers/Player/Wolverine/RawrFuryGain.prototype
        {  12617u, "War Machine" },  // Powers/Player/WarMachine/AutogunMissileEffectThermite.prototype
        {  12618u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelBombsExplosion.prototype
        {  12619u, "Green Goblin" },  // Powers/Player/GreenGoblin/FlyingFlamethrowerHotspotSummon.prototype
        {  12621u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallIcemanSummonAllNewXmen.prototype
        {  12622u, "Venom" },  // Powers/Player/Venom/Traits/OffenseTrait.prototype
        {  12628u, "Silver Surfer" },  // Powers/Player/SilverSurfer/UltimateHiddenPassive.prototype
        {  12630u, "She-Hulk" },  // Powers/Player/SheHulk/BarristerBeatdown.prototype
        {  12631u, "Wolverine" },  // Powers/Player/Wolverine/RapidRegeneration.prototype
        {  12632u, "Psylocke" },  // Powers/Player/Psylocke/DashStealthDecoyPower.prototype
        {  12633u, "Punisher" },  // Powers/Player/Punisher/Rework/PineappleGrenadeDoTCombo.prototype
        {  12634u, "Iron Man" },  // Powers/Player/IronMan/Traits/MechanicTraitSuitPower.prototype
        {  12636u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent1BerserkerHulk.prototype
        {  12638u, "Winter Soldier" },  // Powers/Player/WinterSoldier/EliteWeakenVulnProc.prototype
        {  12640u, "Hawkeye" },  // Powers/Player/Hawkeye/PoisonGasBomb.prototype
        {  12643u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/SpecialForcesSummonProc.prototype
        {  12644u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ShadowBolt.prototype
        {  12646u, "Black Panther" },  // Powers/Player/BlackPanther/TumbleEnd.prototype
        {  12650u, "Hulk" },  // Powers/Player/Hulk/Rework/LeapQuakeTremors.prototype
        {  12651u, "Black Cat" },  // Powers/Player/BlackCat/GasTrapExplosionHotspotEffect.prototype
        {  12655u, "Angela" },  // Powers/Player/Angela/Ultimate.prototype
        {  12656u, "Magik" },  // Powers/Player/Magik/DarkPactDemonVisual.prototype
        {  12662u, "Magik" },  // Powers/Player/Magik/BoneSpiritHiddenPassive.prototype
        {  12663u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelAttackGrant1SR.prototype
        {  12665u, "Magneto" },  // Powers/Player/Magneto/SpawnMetalOrbCombo1To3Orbs.prototype
        {  12666u, "Vision" },  // Powers/Player/Vision/GroundSmash.prototype
        {  12667u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshuSteroidHealthHeal.prototype
        {  12670u, "Iceman" },  // Powers/Player/Iceman/RemoveSnowstormProc.prototype
        {  12674u, "Loki" },  // Powers/Player/Loki/SoulCrushTransfer2.prototype
        {  12677u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ElectricAoEGadget.prototype
        {  12679u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/NovaBurstBonus.prototype
        {  12680u, "Magik" },  // Powers/Player/Magik/LifeTapWeaken.prototype
        {  12682u, "Moon Knight" },  // Powers/Player/MoonKnight/BasicCrescentDart.prototype
        {  12684u, "Colossus" },  // Powers/Player/Colossus/Traits/MechanicTraitArmor.prototype
        {  12685u, "Luke Cage" },  // Powers/Player/LukeCage/Talents/PowerBuffsTauntSteroid.prototype
        {  12686u, "Taskmaster" },  // Powers/Player/Taskmaster/UltimateCaptainAmericaFinestHourEnd.prototype
        {  12687u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/ColleenWing/ColleenWingDefaultAttack2.prototype
        {  12688u, "Hawkeye" },  // Powers/Player/Hawkeye/Talents/MeleeHawkeyeTalent.prototype
        {  12692u, "Blade" },  // Powers/Player/Blade/Shotgun.prototype
        {  12693u, "Carnage" },  // Powers/Player/Carnage/BasicClawsMoveToTargetAsCombo.prototype
        {  12694u, "Vision" },  // Powers/Player/Vision/DenseBuffCancelPower.prototype
        {  12695u, "Loki" },  // Powers/Player/Loki/GlacialSpikeVulnerability.prototype
        {  12698u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondKneeProcEffect.prototype
        {  12699u, "Juggernaut" },  // Powers/Player/Juggernaut/ImInvulnerableRevive.prototype
        {  12700u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Talents/Talent5ChaosBlast.prototype
        {  12701u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/TeamBufferFilterProc.prototype
        {  12702u, "Colossus" },  // Powers/Player/Colossus/MagikSummon/MagikAoE.prototype
        {  12703u, "Thor" },  // Powers/Player/Thor/Rework/HammerThrowOFMissileEffect.prototype
        {  12704u, "Magneto" },  // Powers/Player/Magneto/ElectromagneticShockwaveMissileEffect.prototype
        {  12705u, "Nightcrawler" },  // Powers/Player/Nightcrawler/PBAoESwordSlash.prototype
        {  12707u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ArmyFromNothingAsCombo.prototype
        {  12708u, "War Machine" },  // Powers/Player/WarMachine/BasicLaserBlade.prototype
        {  12721u, "Daredevil" },  // Powers/Player/Daredevil/Talents/OpenerNunchuckStunTalent.prototype
        {  12728u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/SpeedRushJean.prototype
        {  12729u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateJeanSigSummon.prototype
        {  12731u, "Blade" },  // Powers/Player/Blade/ShotgunConeDamageCombo.prototype
        {  12736u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateJeanTKToss.prototype
        {  12740u, "Thor" },  // Powers/Player/Thor/LightningStrikeStun.prototype
        {  12743u, "Thor" },  // Powers/Player/Thor/Rework/SteroidStrongAsCombo.prototype
        {  12744u, "Wolverine" },  // Powers/Player/Wolverine/Talents/Talent1FuryGenSpenderDmg.prototype
        {  12745u, "Venom" },  // Powers/Player/Venom/Yank.prototype
        {  12747u, "Cyclops" },  // Powers/Player/Cyclops/Talents/SigMaximumOpticsTalent.prototype
        {  12748u, "Black Widow" },  // Powers/Player/BlackWidow/ElectricBatonsSecondHit.prototype
        {  12749u, "Rogue" },  // Powers/Player/Rogue/DiveBomb.prototype
        {  12750u, "X-23" },  // Powers/Player/X23/BasicBloody.prototype
        {  12751u, "Nova" },  // Powers/Player/Nova/NovaForceHiddenPassive.prototype
        {  12756u, "Cable" },  // Powers/Player/Cable/Talents/ParticleAcceleratorBuff.prototype
        {  12757u, "Green Goblin" },  // Powers/Player/GreenGoblin/MachineGunsMissileEffect.prototype
        {  12759u, "Hawkeye" },  // Powers/Player/Hawkeye/DisengagingShotCombo.prototype
        {  12764u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent5AssassinateCDR.prototype
        {  12766u, "Captain America" },  // Powers/Player/CaptainAmerica/VibraniumBash.prototype
        {  12767u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/BurrowMovement.prototype
        {  12768u, "Green Goblin" },  // Powers/Player/GreenGoblin/PbAoESpin.prototype
        {  12770u, "Juggernaut" },  // Powers/Player/Juggernaut/UnstoppableChargeComboSummon.prototype
        {  12771u, "Blade" },  // Powers/Player/Blade/KnifeBarrageFinisherMissileEffect.prototype
        {  12772u, "Magneto" },  // Powers/Player/Magneto/DebrisVisualPhase3.prototype
        {  12774u, "Magneto" },  // Powers/Player/Magneto/ForceFieldSummonCombo.prototype
        {  12779u, "Nova" },  // Powers/Player/Nova/BasicPunch.prototype
        {  12780u, "Black Bolt" },  // Powers/Player/BlackBolt/PummelStartCombo.prototype
        {  12781u, "Deadpool" },  // Powers/Player/Deadpool/Rework/PowerUpWingedShoesEffect.prototype
        {  12782u, "Cable" },  // Powers/Player/Cable/FutureBombEnergyExplosion.prototype
        {  12785u, "Storm" },  // Powers/Player/Storm/Talents/TyphoonAcidRain.prototype
        {  12788u, "Luke Cage" },  // Powers/Player/LukeCage/Charge.prototype
        {  12789u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/OverheatedSingleTargets.prototype
        {  12791u, "Iron Fist" },  // Powers/Player/IronFist/ChiSteroidCraneStanceBuff.prototype
        {  12795u, "Wolverine" },  // Powers/Player/Wolverine/ReviveFuryGain.prototype
        {  12796u, "Iceman" },  // Powers/Player/Iceman/Traits/ArmorRegen.prototype
        {  12797u, "Thing" },  // Powers/Player/Thing/Rework/RockslideChargeCombo.prototype
        {  12799u, "Nick Fury" },  // Powers/Player/NickFury/SteroidCombo.prototype
        {  12805u, "Deadpool" },  // Powers/Player/Deadpool/Traits/MechanicTraitAwesomeProcEffect.prototype
        {  12806u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoAoEAttack.prototype
        {  12811u, "X-23" },  // Powers/Player/X23/UltBuffComboEffect.prototype
        {  12812u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassivePunisherBuffProcEffect.prototype
        {  12814u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Talents/BackstabStealthTalent.prototype
        {  12815u, "Deadpool" },  // Powers/Player/Deadpool/Rework/StrafeMeleeHotspotDoT.prototype
        {  12816u, "Hawkeye" },  // Powers/Player/Hawkeye/UltimateSummon.prototype
        {  12824u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LukeCagePummel.prototype
        {  12826u, "Loki" },  // Powers/Player/Loki/Talents/FourRealmsInfernalBinding.prototype
        {  12831u, "Elektra" },  // Powers/Player/Elektra/Talents/KnifeRopeMastery.prototype
        {  12832u, "She-Hulk" },  // Powers/Player/SheHulk/Traits/MechanicTraitAngerBuffEffectIncrease.prototype
        {  12835u, "Thor" },  // Powers/Player/Thor/Rework/BigDFACombo.prototype
        {  12837u, "Cyclops" },  // Powers/Player/Cyclops/Traits/OffenseTrait.prototype
        {  12838u, "Magik" },  // Powers/Player/Magik/Teleport.prototype
        {  12840u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/OverheatMorePotent.prototype
        {  12843u, "Psylocke" },  // Powers/Player/Psylocke/ConeBlastDecoy.prototype
        {  12845u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveKurse.prototype
        {  12847u, "Black Panther" },  // Powers/Player/BlackPanther/Tumble.prototype
        {  12848u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Talents/Talent4SpinningMinesFreeCast.prototype
        {  12850u, "Moon Knight" },  // Powers/Player/MoonKnight/BasicCrescentDartMissileEffect.prototype
        {  12853u, "Iceman" },  // Powers/Player/Iceman/Icicle.prototype
        {  12854u, "Captain America" },  // Powers/Player/CaptainAmerica/OnBroadStrikeBlockGainPips.prototype
        {  12858u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ChanneledBeamEndCondition.prototype
        {  12861u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Ultimate.prototype
        {  12863u, "Nova" },  // Powers/Player/Nova/LungingPunchDashPower.prototype
        {  12865u, "Captain America" },  // Powers/Player/CaptainAmerica/UltimateChanneledBeamHotspotEffect.prototype
        {  12866u, "Blade" },  // Powers/Player/Blade/KnifeBarrageFinisherMissilePower.prototype
        {  12869u, "Cyclops" },  // Powers/Player/Cyclops/Talents/MagnetoCallinTalent.prototype
        {  12874u, "Black Bolt" },  // Powers/Player/BlackBolt/ChanneledBeamVulnerability.prototype
        {  12879u, "Magneto" },  // Powers/Player/Magneto/ElectromagneticBeamBuff.prototype
        {  12882u, "Hulk" },  // Powers/Player/Hulk/Rework/ShockwaveLargeVisualCombo.prototype
        {  12883u, "Punisher" },  // Powers/Player/Punisher/Rework/AutomaticShotgunProcEffect.prototype
        {  12887u, "Rogue" },  // Powers/Player/Rogue/Talents/HealthDefenseBuff.prototype
        {  12889u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PullUnderDamageCombo.prototype
        {  12894u, "Juggernaut" },  // Powers/Player/Juggernaut/BigChargeInstantKillCombo.prototype
        {  12895u, "Angela" },  // Powers/Player/Angela/DFAStart.prototype
        {  12897u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/BigFistedPunch.prototype
        {  12898u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TelepathicIllusionPhoenixHotspotSummonCombo.prototype
        {  12899u, "Angela" },  // Powers/Player/Angela/DFACombo1Used.prototype
        {  12905u, "Venom" },  // Powers/Player/Venom/SigFreakoutSymbioteSummon.prototype
        {  12906u, "Venom" },  // Powers/Player/Venom/SigFreakoutIchorSpikeProcBuff.prototype
        {  12910u, "Angela" },  // Powers/Player/Angela/Traits/DefenseTrait.prototype
        {  12912u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ChanneledBeamBuffPhase3Start.prototype
        {  12915u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/MistyKnight/ConcussiveBlastHotspotEffect.prototype
        {  12916u, "Colossus" },  // Powers/Player/Colossus/GroundStomp.prototype
        {  12917u, "Loki" },  // Powers/Player/Loki/UltimateMeleeAttack.prototype
        {  12921u, "Hawkeye" },  // Powers/Player/Hawkeye/DodgePassiveMedkitHealingProc.prototype
        {  12922u, "Green Goblin" },  // Powers/Player/GreenGoblin/Talents/GoblinLaserDamageMultTalent.prototype
        {  12923u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/TippyToe.prototype
        {  12926u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/ElektraMarkForDeathProc.prototype
        {  12927u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseToggleHealthOnHitProc.prototype
        {  12933u, "Magik" },  // Powers/Player/Magik/Talents/Talent5OtherworldlyNovaDemonBuff.prototype
        {  12934u, "Blade" },  // Powers/Player/Blade/Disembowel.prototype
        {  12936u, "Psylocke" },  // Powers/Player/Psylocke/KatanaDoubleStrikeMissileEffect.prototype
        {  12937u, "Blade" },  // Powers/Player/Blade/Talents/BerserkerTalent.prototype
        {  12940u, "Luke Cage" },  // Powers/Player/LukeCage/UltimateBoulderBeatdownHotspotEffect.prototype
        {  12941u, "Black Bolt" },  // Powers/Player/BlackBolt/EnergyDecay.prototype
        {  12942u, "Jean Grey" },  // Powers/Player/JeanGrey/PhoenixModeToggleHealthOnHitProc.prototype
        {  12943u, "Punisher" },  // Powers/Player/Punisher/Rework/FlashbangLauncher.prototype
        {  12948u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/DarkPhoenixBonus.prototype
        {  12952u, "Punisher" },  // Powers/Player/Punisher/Talents/SniperRifleBuff.prototype
        {  12956u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveDeadpool.prototype
        {  12962u, "Vision" },  // Powers/Player/Vision/EnhanceRobotBuffAlwaysOnTalentHiddenPassive.prototype
        {  12964u, "Psylocke" },  // Powers/Player/Psylocke/LungeReduceDashstealthCD.prototype
        {  12965u, "Gambit" },  // Powers/Player/Gambit/BasicKineticCard.prototype
        {  12968u, "Cable" },  // Powers/Player/Cable/VeteranWarriorMentalBuff.prototype
        {  12969u, "Magik" },  // Powers/Player/Magik/SorcerousEruptionDamageComboLarge.prototype
        {  12970u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveGreenGoblin.prototype
        {  12971u, "Carnage" },  // Powers/Player/Carnage/AxeThrowMissileEffect.prototype
        {  12973u, "Beast" },  // Powers/Player/Beast/ShieldGadgetOnChainKillKnockback.prototype
        {  12976u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/DoubleTimeTalent.prototype
        {  12980u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GambitRaginCajun.prototype
        {  12983u, "Magik" },  // Powers/Player/Magik/BounceStrikeStart.prototype
        {  12984u, "Taskmaster" },  // Powers/Player/Taskmaster/UltimateDaredevilShadowStrikeMovement.prototype
        {  12985u, "Blade" },  // Powers/Player/Blade/HemoglycerinGauntlet6thAttack.prototype
        {  12987u, "Loki" },  // Powers/Player/Loki/UnveiledHotspotEffect.prototype
        {  12988u, "Doctor Strange" },  // Powers/Player/DoctorStrange/MirrorImage.prototype
        {  12991u, "Punisher" },  // Powers/Player/Punisher/Rework/ReloadOutOfCombatProcEffect.prototype
        {  12992u, "Black Widow" },  // Powers/Player/BlackWidow/ElectricBatonsChainLightningCombo.prototype
        {  12995u, "Cyclops" },  // Powers/Player/Cyclops/SignatureBeamRangedBuff.prototype
        {  12996u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/MysticismRestorationProcEffectDelay.prototype
        {  12997u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveBullseye.prototype
        {  13002u, "Luke Cage" },  // Powers/Player/LukeCage/SummonAllHeroesForHire.prototype
        {  13003u, "Black Widow" },  // Powers/Player/BlackWidow/UltimateAgentAShotgunBlast.prototype
        {  13004u, "Dr. Doom" },  // Powers/Player/DrDoom/RapidFireMissileEffect.prototype
        {  13006u, "Hawkeye" },  // Powers/Player/Hawkeye/ElectricQuiverEnabled.prototype
        {  13008u, "Nick Fury" },  // Powers/Player/NickFury/RocketLauncherKeywordConditionCombo.prototype
        {  13010u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotThumperCallInGroundPoundHSEffect.prototype
        {  13011u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/IronFistTechniqueSnakeStanceBuff.prototype
        {  13012u, "Winter Soldier" },  // Powers/Player/WinterSoldier/OnCritEnduranceProc.prototype
        {  13013u, "Luke Cage" },  // Powers/Player/LukeCage/Traits/OffenseTrait.prototype
        {  13014u, "Dr. Doom" },  // Powers/Player/DrDoom/DoomsDayAirStrikeExplosion.prototype
        {  13017u, "Ultron" },  // Powers/Player/Ultron/EncephaloBeamConfuseCombo.prototype
        {  13021u, "Juggernaut" },  // Powers/Player/Juggernaut/Traits/OffenseTrait.prototype
        {  13023u, "Ultron" },  // Powers/Player/Ultron/Talents/Talent5ConcussionBlastBuff.prototype
        {  13026u, "Elektra" },  // Powers/Player/Elektra/TeleportDash.prototype
        {  13030u, "Ultron" },  // Powers/Player/Ultron/SignatureHotspotEffect.prototype
        {  13034u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ShadowBoltAsProc.prototype
        {  13040u, "Captain America" },  // Powers/Player/CaptainAmerica/HeroicStrikeShieldSwipeConditionCancelCombo.prototype
        {  13043u, "Ant-Man" },  // Powers/Player/AntMan/RedHotsPassiveAttackProc.prototype
        {  13045u, "She-Hulk" },  // Powers/Player/SheHulk/ObjectionCritChanceComboTalent.prototype
        {  13048u, "War Machine" },  // Powers/Player/WarMachine/ChaingunFullAutoMissileEffectThermite.prototype
        {  13049u, "Human Torch" },  // Powers/Player/HumanTorch/ConsumeEatHotspots.prototype
        {  13054u, "Juggernaut" },  // Powers/Player/Juggernaut/BasicPunchCDREffect.prototype
        {  13058u, "Angela" },  // Powers/Player/Angela/SwordPummel5thAttack.prototype
        {  13060u, "Venom" },  // Powers/Player/Venom/MeleePassiveProcEffect.prototype
        {  13062u, "Storm" },  // Powers/Player/Storm/StormSurgeEnduranceRestore.prototype
        {  13063u, "Moon Knight" },  // Powers/Player/MoonKnight/Ultimate.prototype
        {  13065u, "Hawkeye" },  // Powers/Player/Hawkeye/FreezingQuiverEnabled.prototype
        {  13066u, "Iron Man" },  // Powers/Player/IronMan/UltimateChanEnergyBeamEffect.prototype
        {  13072u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/BasicStretchyPunch.prototype
        {  13073u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/DrDoomBallLightningEffect.prototype
        {  13074u, "Carnage" },  // Powers/Player/Carnage/Traits/OffenseTraitStatConversionEffect.prototype
        {  13075u, "Captain America" },  // Powers/Player/CaptainAmerica/BrutalStrikeEffect.prototype
        {  13076u, "Ant-Man" },  // Powers/Player/AntMan/AntSpenderSpiritRestore4pct.prototype
        {  13077u, "She-Hulk" },  // Powers/Player/SheHulk/Traits/DefenseTrait.prototype
        {  13079u, "Ant-Man" },  // Powers/Player/AntMan/MultiStrikeEnd.prototype
        {  13081u, "Blade" },  // Powers/Player/Blade/PBAoEGlaiveCooldown.prototype
        {  13082u, "Nova" },  // Powers/Player/Nova/ChanneledPulsarBeamEffect.prototype
        {  13083u, "Storm" },  // Powers/Player/Storm/SurgeGainOverTime.prototype
        {  13084u, "Nova" },  // Powers/Player/Nova/Talents/Talent3MicroMagnetonPulsar.prototype
        {  13085u, "Black Cat" },  // Powers/Player/BlackCat/SignatureBleed.prototype
        {  13087u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MordoMistsDoT.prototype
        {  13093u, "Nick Fury" },  // Powers/Player/NickFury/BulletSpray.prototype
        {  13094u, "Hulk" },  // Powers/Player/Hulk/Rework/RawrBerserkerBuff.prototype
        {  13095u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/LiftAndSlamJeanDamage.prototype
        {  13098u, "Punisher" },  // Powers/Player/Punisher/Rework/ShotgunDoT.prototype
        {  13102u, "Angela" },  // Powers/Player/Angela/Constrict.prototype
        {  13104u, "Nova" },  // Powers/Player/Nova/PulsarExplosionSpiritRestoration.prototype
        {  13108u, "Ultron" },  // Powers/Player/Ultron/DashProcEffect.prototype
        {  13110u, "Magik" },  // Powers/Player/Magik/Talents/Talent2LifeTapAmpDamage.prototype
        {  13111u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/AstralProjectionTwincast.prototype
        {  13113u, "Moon Knight" },  // Powers/Player/MoonKnight/StrafeMissileWithShadow.prototype
        {  13117u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ImplodeExplodeOutCombo.prototype
        {  13118u, "Black Widow" },  // Powers/Player/BlackWidow/SwingingAssaultKnockdownEffect.prototype
        {  13119u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ConePunchSTSS.prototype
        {  13121u, "Black Cat" },  // Powers/Player/BlackCat/TaserTrap.prototype
        {  13122u, "Nova" },  // Powers/Player/Nova/PulsarHotspotSlow.prototype
        {  13125u, "She-Hulk" },  // Powers/Player/SheHulk/ObjectionMissileEffect.prototype
        {  13126u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/UltimateStopAcornMeteors.prototype
        {  13128u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Talents/Talent2PlasmaCannonBuff.prototype
        {  13130u, "Storm" },  // Powers/Player/Storm/StormSurgeLightningTempestSummonAgent.prototype
        {  13132u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/JeanFormSpec.prototype
        {  13133u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/IronFistTechnique.prototype
        {  13134u, "Nick Fury" },  // Powers/Player/NickFury/Reload.prototype
        {  13138u, "Human Torch" },  // Powers/Player/HumanTorch/OverheatHotspotEffect.prototype
        {  13139u, "Deadpool" },  // Powers/Player/Deadpool/CleverGirlDollTaunt.prototype
        {  13140u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/RobbieReyesDriveBy.prototype
        {  13142u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent4CarFistsBonus.prototype
        {  13145u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/AutoSingularity.prototype
        {  13150u, "Black Cat" },  // Powers/Player/BlackCat/ShrapnelTrap.prototype
        {  13152u, "Black Widow" },  // Powers/Player/BlackWidow/RollingGrenades9.prototype
        {  13155u, "Iceman" },  // Powers/Player/Iceman/FrozenOrbTooltipDriver.prototype
        {  13156u, "Elektra" },  // Powers/Player/Elektra/KillCommandMysticArcaneBeam.prototype
        {  13157u, "Black Bolt" },  // Powers/Player/BlackBolt/Talent1EnergyPassiveRangedBuff.prototype
        {  13162u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/PunisherTenacityTalent.prototype
        {  13164u, "Loki" },  // Powers/Player/Loki/SearingEmbers.prototype
        {  13166u, "Winter Soldier" },  // Powers/Player/WinterSoldier/GrenadeLauncherDoT.prototype
        {  13168u, "Dr. Doom" },  // Powers/Player/DrDoom/Talents/Talent4TechnicalTyrantCooldownRed.prototype
        {  13170u, "Punisher" },  // Powers/Player/Punisher/Rework/BackwardsTumble.prototype
        {  13171u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MagikMiniDemonDoubleStrike.prototype
        {  13178u, "Nick Fury" },  // Powers/Player/NickFury/DriveByAnimStart.prototype
        {  13180u, "Cyclops" },  // Powers/Player/Cyclops/Rework/ConeBeamMelee.prototype
        {  13181u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SecondaryResourceResetDaredevil.prototype
        {  13187u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/HeatGeneration.prototype
        {  13188u, "Nova" },  // Powers/Player/Nova/PulsarHotspotRandomLocation.prototype
        {  13190u, "Beast" },  // Powers/Player/Beast/DeathFromAboveEnd.prototype
        {  13192u, "Taskmaster" },  // Powers/Player/Taskmaster/WidowsBite.prototype
        {  13194u, "Psylocke" },  // Powers/Player/Psylocke/PsiKnifeTripleStrike.prototype
        {  13198u, "Iron Man" },  // Powers/Player/IronMan/ShieldOverloadExplosionUpgrade2.prototype
        {  13200u, "War Machine" },  // Powers/Player/WarMachine/AlphaStrikeMissiles.prototype
        {  13202u, "Captain America" },  // Powers/Player/CaptainAmerica/Talents/GuardedTeamBuffSpec.prototype
        {  13205u, "Colossus" },  // Powers/Player/Colossus/MetalChargeCollisionEffect.prototype
        {  13206u, "Angela" },  // Powers/Player/Angela/HevenlyWrathPowerCostModifierClear.prototype
        {  13215u, "Angela" },  // Powers/Player/Angela/SwordPummel3rdAttack.prototype
        {  13217u, "Vision" },  // Powers/Player/Vision/ScorchedEarthHotspots.prototype
        {  13218u, "Storm" },  // Powers/Player/Storm/ChainLightningDestructibleKiller.prototype
        {  13220u, "Thing" },  // Powers/Player/Thing/Rework/ParkingMeterSmash.prototype
        {  13221u, "Hawkeye" },  // Powers/Player/Hawkeye/ExplosiveArrowMissileEffect.prototype
        {  13222u, "Doctor Strange" },  // Powers/Player/DoctorStrange/FangNukeMissileEffect.prototype
        {  13229u, "Taskmaster" },  // Powers/Player/Taskmaster/SteroidHotspotPointToTaskmaster.prototype
        {  13230u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/GiantGunGadgetHiddenPassive.prototype
        {  13232u, "Black Panther" },  // Powers/Player/BlackPanther/DaggerChargeSummonCombo.prototype
        {  13233u, "Angela" },  // Powers/Player/Angela/BowPBAoEVulnCombo.prototype
        {  13234u, "Cyclops" },  // Powers/Player/Cyclops/BasicPunchHealingComboEffect.prototype
        {  13236u, "Thing" },  // Powers/Player/Thing/Rework/LampBatThrowDisableDecayCombo.prototype
        {  13237u, "Beast" },  // Powers/Player/Beast/TeslaTowerGadgetLightningStrike.prototype
        {  13238u, "Captain America" },  // Powers/Player/CaptainAmerica/FuriousLungeEffect.prototype
        {  13239u, "Black Cat" },  // Powers/Player/BlackCat/NineLivesHealthMinHiddenPassive.prototype
        {  13241u, "Gambit" },  // Powers/Player/Gambit/CardPickupDelayBeforeRemoveCards.prototype
        {  13246u, "Juggernaut" },  // Powers/Player/Juggernaut/PreventMomentumDecay750MS.prototype
        {  13247u, "Venom" },  // Powers/Player/Venom/MawFromAbove.prototype
        {  13249u, "Deadpool" },  // Powers/Player/Deadpool/HealthRegenPassiveTooltipDriver.prototype
        {  13256u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerSummonBaseLady.prototype
        {  13260u, "Cyclops" },  // Powers/Player/Cyclops/Rework/SignatureBeam.prototype
        {  13266u, "Black Panther" },  // Powers/Player/BlackPanther/Traits/DefensePassive.prototype
        {  13267u, "Iron Man" },  // Powers/Player/IronMan/DamageAbsorptionShieldEnduranceCost.prototype
        {  13268u, "Storm" },  // Powers/Player/Storm/MicroburstKeywordConditionComboBigger.prototype
        {  13269u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent3CallinSharedCooldown.prototype
        {  13270u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/AlterReality.prototype
        {  13272u, "Moon Knight" },  // Powers/Player/MoonKnight/BrutalChanceTerrifyTributeGain.prototype
        {  13273u, "Thing" },  // Powers/Player/Thing/Traits/ClobberinTimeStackingBuff.prototype
        {  13274u, "Nova" },  // Powers/Player/Nova/PulsarKillProcEffect.prototype
        {  13275u, "Magik" },  // Powers/Player/Magik/BoneSpirit.prototype
        {  13276u, "Beast" },  // Powers/Player/Beast/SignatureJubileeCallIn.prototype
        {  13279u, "Magneto" },  // Powers/Player/Magneto/ElectromagneticBeam.prototype
        {  13292u, "Doctor Strange" },  // Powers/Player/DoctorStrange/MirrorImageSummonCombo.prototype
        {  13293u, "Emma Frost" },  // Powers/Player/EmmaFrost/UltimateTRexRoar.prototype
        {  13294u, "Hulk" },  // Powers/Player/Hulk/Rework/CarFistsExplosion.prototype
        {  13296u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/DisengagingShot.prototype
        {  13298u, "Ant-Man" },  // Powers/Player/AntMan/ParticleOverchargeAntBuff.prototype
        {  13301u, "Loki" },  // Powers/Player/Loki/IceShard.prototype
        {  13302u, "Thor" },  // Powers/Player/Thor/Rework/SignatureAntiforce.prototype
        {  13304u, "Black Bolt" },  // Powers/Player/BlackBolt/HypersonicScreamMobInstaKill.prototype
        {  13307u, "Vision" },  // Powers/Player/Vision/ControlRobotComboControlTarget.prototype
        {  13309u, "Moon Knight" },  // Powers/Player/MoonKnight/TributeGainInCombat.prototype
        {  13313u, "Carnage" },  // Powers/Player/Carnage/LowArmorChaosBuffRemoval.prototype
        {  13314u, "Cable" },  // Powers/Player/Cable/EyeForWeakness.prototype
        {  13315u, "Dr. Doom" },  // Powers/Player/DrDoom/DoomBotsProc.prototype
        {  13317u, "Gambit" },  // Powers/Player/Gambit/JacksOrBetter.prototype
        {  13322u, "Dr. Doom" },  // Powers/Player/DrDoom/ElectricBlastBuffDamageCombo.prototype
        {  13328u, "Angela" },  // Powers/Player/Angela/Talents/DFAExtraHitTalent.prototype
        {  13332u, "Blade" },  // Powers/Player/Blade/SerumInjectionRezInvulnCombo.prototype
        {  13339u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TKTossDamageReductionCombo.prototype
        {  13342u, "Iron Fist" },  // Powers/Player/IronFist/Talents/Talent2ComboChiBurst.prototype
        {  13343u, "X-23" },  // Powers/Player/X23/Talents/RemoveFerocity.prototype
        {  13345u, "Jean Grey" },  // Powers/Player/JeanGrey/DebrisMaelstrom.prototype
        {  13348u, "Gambit" },  // Powers/Player/Gambit/Talents/Talent3DeucesWild.prototype
        {  13352u, "Cable" },  // Powers/Player/Cable/Talents/IllusionLayer.prototype
        {  13354u, "She-Hulk" },  // Powers/Player/SheHulk/OpeningStatementComboPointGain.prototype
        {  13355u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BamfDiveBomb.prototype
        {  13358u, "Emma Frost" },  // Powers/Player/EmmaFrost/Traits/DiamondFormActivate.prototype
        {  13361u, "Luke Cage" },  // Powers/Player/LukeCage/SummonAllBuddies.prototype
        {  13362u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent2DefenseBuff.prototype
        {  13364u, "Iceman" },  // Powers/Player/Iceman/ShowOffStatueExplosionAsSummon.prototype
        {  13366u, "Ghost Rider" },  // Powers/Player/GhostRider/InfernalContractCombo.prototype
        {  13368u, "Vision" },  // Powers/Player/Vision/PhasePunch.prototype
        {  13370u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveSpiderman.prototype
        {  13371u, "Venom" },  // Powers/Player/Venom/MeleePassiveHiddenPassive.prototype
        {  13372u, "Iceman" },  // Powers/Player/Iceman/ChillAsIntervalPower.prototype
        {  13375u, "Ghost Rider" },  // Powers/Player/GhostRider/HellfireCombustion.prototype
        {  13379u, "Juggernaut" },  // Powers/Player/Juggernaut/UltimateStopQuakes.prototype
        {  13380u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Talents/Talent3FurtherChaos.prototype
        {  13382u, "Dr. Doom" },  // Powers/Player/DrDoom/Talents/Talent1MagicLanceBasicPunch.prototype
        {  13383u, "Captain America" },  // Powers/Player/CaptainAmerica/ShieldBashCombo.prototype
        {  13384u, "Magik" },  // Powers/Player/Magik/SummonBFLD.prototype
        {  13385u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotThumperCallIn.prototype
        {  13386u, "Iron Fist" },  // Powers/Player/IronFist/Traits/MechanicTraitChi.prototype
        {  13388u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/TumbleSprintEffect.prototype
        {  13389u, "X-23" },  // Powers/Player/X23/HealCleanseSelfRezCooldownDisplay.prototype
        {  13391u, "Hawkeye" },  // Powers/Player/Hawkeye/Talents/AutoTrickArrows.prototype
        {  13394u, "Human Torch" },  // Powers/Player/HumanTorch/ChanneledEnergyBeamFireballProc.prototype
        {  13395u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/BurrowEnd.prototype
        {  13396u, "Storm" },  // Powers/Player/Storm/UltimateBoltEffect.prototype
        {  13397u, "Iron Fist" },  // Powers/Player/IronFist/Talents/Talent4ChiOverload.prototype
        {  13403u, "Wolverine" },  // Powers/Player/Wolverine/SignatureDashSlashBrutalCOmbo.prototype
        {  13405u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzyRanged.prototype
        {  13406u, "Ghost Rider" },  // Powers/Player/GhostRider/BasicFireballDoTStack.prototype
        {  13415u, "Daredevil" },  // Powers/Player/Daredevil/Update/BlockRatingCombo.prototype
        {  13417u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Talents/Talent5BFGBuff.prototype
        {  13419u, "X-23" },  // Powers/Player/X23/TripleKick.prototype
        {  13420u, "Angela" },  // Powers/Player/Angela/RibbonChannelHotspotEffect.prototype
        {  13424u, "Black Panther" },  // Powers/Player/BlackPanther/Talents/SweepBleedDamage.prototype
        {  13425u, "Thor" },  // Powers/Player/Thor/SteroidStrongHealProc.prototype
        {  13427u, "Blade" },  // Powers/Player/Blade/SerumInjectionSteroidBuffRotational.prototype
        {  13428u, "Captain America" },  // Powers/Player/CaptainAmerica/PassiveSuperSoldierHiddenPassive.prototype
        {  13432u, "Blade" },  // Powers/Player/Blade/SerumInjectionHealing.prototype
        {  13434u, "Ultron" },  // Powers/Player/Ultron/RadiationBlastPulse.prototype
        {  13440u, "Vision" },  // Powers/Player/Vision/DeathfromBelowComboEffectKnockdown.prototype
        {  13446u, "Doctor Strange" },  // Powers/Player/DoctorStrange/VaporsWindsOfWatoombBuff.prototype
        {  13448u, "Luke Cage" },  // Powers/Player/LukeCage/PummelCCImmuneCombo.prototype
        {  13449u, "Jean Grey" },  // Powers/Player/JeanGrey/PanicPhoenix.prototype
        {  13452u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/ForcePushDamageAbsorptionShield.prototype
        {  13454u, "Iceman" },  // Powers/Player/Iceman/IceGolemTwoHandedPound.prototype
        {  13457u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CloseCombat.prototype
        {  13463u, "War Machine" },  // Powers/Player/WarMachine/SpecCloakingDeviceGunBuffEffect.prototype
        {  13465u, "Juggernaut" },  // Powers/Player/Juggernaut/UnstoppableChargeInfinite.prototype
        {  13467u, "Dr. Doom" },  // Powers/Player/DrDoom/Talents/Talent4LordSignatureRemap.prototype
        {  13469u, "Juggernaut" },  // Powers/Player/Juggernaut/MomentumDecay.prototype
        {  13476u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceGainCombo20.prototype
        {  13481u, "Colossus" },  // Powers/Player/Colossus/CallNightcrawler.prototype
        {  13486u, "Winter Soldier" },  // Powers/Player/WinterSoldier/StealthRollStealth.prototype
        {  13491u, "Dr. Doom" },  // Powers/Player/DrDoom/Traits/MechanicTraitPowerMagic.prototype
        {  13495u, "Beast" },  // Powers/Player/Beast/PummelBrutalCombo.prototype
        {  13498u, "Black Bolt" },  // Powers/Player/BlackBolt/GapCloseDamageCombo.prototype
        {  13502u, "Loki" },  // Powers/Player/Loki/IllusionFromAboveDecoyPower.prototype
        {  13503u, "Nick Fury" },  // Powers/Player/NickFury/TumbleStealth.prototype
        {  13504u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedChargeEnd.prototype
        {  13505u, "Kitty Pryde" },  // Powers/Player/KittyPryde/BasicMelee.prototype
        {  13506u, "Iceman" },  // Powers/Player/Iceman/Traits/FrostArmorHiddenPassive.prototype
        {  13507u, "Moon Knight" },  // Powers/Player/MoonKnight/Talents/BrutalChanceTerrifyBonusCrit.prototype
        {  13511u, "Iron Man" },  // Powers/Player/IronMan/OrbitalBombardmentRandomStrikeCombo.prototype
        {  13512u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Reconstruction.prototype
        {  13516u, "Moon Knight" },  // Powers/Player/MoonKnight/HealthDefenseOnGotHitTributeGain.prototype
        {  13518u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ShockwavePBAoE.prototype
        {  13519u, "Black Panther" },  // Powers/Player/BlackPanther/AcrobaticAttackComboDamageCombo.prototype
        {  13522u, "Nova" },  // Powers/Player/Nova/DeathFromAboveEnd.prototype
        {  13524u, "Elektra" },  // Powers/Player/Elektra/Talents/StealthTeamBuffTalent.prototype
        {  13525u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveBeast.prototype
        {  13526u, "Storm" },  // Powers/Player/Storm/MicroburstRingDamageComboBigger.prototype
        {  13532u, "Silver Surfer" },  // Powers/Player/SilverSurfer/TeleportDashRefundEndurance.prototype
        {  13533u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TKTossPhoenixMoreMissilesVisualCombo.prototype
        {  13539u, "Nova" },  // Powers/Player/Nova/ChanneledPulsarBeam.prototype
        {  13540u, "Cable" },  // Powers/Player/Cable/TKOverloadKnockdown.prototype
        {  13550u, "Ant-Man" },  // Powers/Player/AntMan/ShrinkingStrike.prototype
        {  13551u, "Carnage" },  // Powers/Player/Carnage/AxeBleedHiddenPassive.prototype
        {  13555u, "Kitty Pryde" },  // Powers/Player/KittyPryde/UltimateComboRight.prototype
        {  13561u, "Venom" },  // Powers/Player/Venom/Talents/HealthCostIncrease.prototype
        {  13567u, "Cable" },  // Powers/Player/Cable/FutureBombMentalExplosionPsimitarKeyword.prototype
        {  13569u, "Vision" },  // Powers/Player/Vision/HealingNanitesRobotHealingCombo.prototype
        {  13572u, "Cable" },  // Powers/Player/Cable/TelepathicIllusionDoT.prototype
        {  13574u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TelepathicIllusionPhoenixDeathProc.prototype
        {  13576u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent4CallInBuffsKitty.prototype
        {  13579u, "Cable" },  // Powers/Player/Cable/PlasmaBarrage.prototype
        {  13580u, "Iron Man" },  // Powers/Player/IronMan/Talents/UpgradeArcReactor.prototype
        {  13588u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/SheHulkObjectionMissileEffect.prototype
        {  13589u, "Thing" },  // Powers/Player/Thing/YancyStreetGangPassive.prototype
        {  13590u, "Taskmaster" },  // Powers/Player/Taskmaster/WebSplatVulnerabilityEffect.prototype
        {  13591u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Traits/DefaultAmmoRegenTrigger.prototype
        {  13595u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/CleaSummonFlamesSummonLocusCombo.prototype
        {  13597u, "Iron Man" },  // Powers/Player/IronMan/RainOfMissiles.prototype
        {  13600u, "Punisher" },  // Powers/Player/Punisher/Rework/Flashbang.prototype
        {  13603u, "Carnage" },  // Powers/Player/Carnage/MegaClaw.prototype
        {  13606u, "Psylocke" },  // Powers/Player/Psylocke/KatanaLeapSlashAoEDecoyDoT.prototype
        {  13609u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/IcemanFrozenOrbMissileEffect.prototype
        {  13610u, "Human Torch" },  // Powers/Player/HumanTorch/FlameTornadoKnockup.prototype
        {  13611u, "War Machine" },  // Powers/Player/WarMachine/Warhead.prototype
        {  13612u, "Ultron" },  // Powers/Player/Ultron/UltimateHiddenPassive.prototype
        {  13614u, "Hawkeye" },  // Powers/Player/Hawkeye/TenArrowSpeedLoaderDamageShieldCombo.prototype
        {  13617u, "Cyclops" },  // Powers/Player/Cyclops/BouncingBeamDestructibleKiller.prototype
        {  13619u, "Winter Soldier" },  // Powers/Player/WinterSoldier/SniperShot.prototype
        {  13623u, "Human Torch" },  // Powers/Player/HumanTorch/NovaChargeKnockdown.prototype
        {  13625u, "Iceman" },  // Powers/Player/Iceman/FrozenOrbSummonHotspot.prototype
        {  13626u, "Captain America" },  // Powers/Player/CaptainAmerica/SoundRicochetSerum.prototype
        {  13629u, "Venom" },  // Powers/Player/Venom/SymbioteDrainPowerWhite2.prototype
        {  13631u, "Nova" },  // Powers/Player/Nova/Traits/DefenseTrait.prototype
        {  13632u, "Blade" },  // Powers/Player/Blade/StackableBleedRangedArea.prototype
        {  13633u, "Thor" },  // Powers/Player/Thor/Rework/HammerDashCombo.prototype
        {  13634u, "Daredevil" },  // Powers/Player/Daredevil/OpenerWeaken.prototype
        {  13635u, "Loki" },  // Powers/Player/Loki/IllusionFromAbove.prototype
        {  13637u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/Pummel1stAttack.prototype
        {  13638u, "Ghost Rider" },  // Powers/Player/GhostRider/Talents/Talent3ProtectTheInnocent.prototype
        {  13639u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent4SurpriseWitnessCDR.prototype
        {  13643u, "Carnage" },  // Powers/Player/Carnage/AxeThrow.prototype
        {  13645u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/UltimateBubblestormHotspotEffect.prototype
        {  13646u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ShieldedFistMissileEffectEnhanced.prototype
        {  13647u, "Daredevil" },  // Powers/Player/Daredevil/BouncingStrikeHideMeshInvuln.prototype
        {  13650u, "Angela" },  // Powers/Player/Angela/DoubleAxeThrowMissileEffect.prototype
        {  13655u, "Captain America" },  // Powers/Player/CaptainAmerica/ShieldStrikeTalentDeflectCondition.prototype
        {  13656u, "Venom" },  // Powers/Player/Venom/MeleeBasic.prototype
        {  13657u, "Black Bolt" },  // Powers/Player/BlackBolt/Talents/Talent1GapCloseRemap.prototype
        {  13659u, "Elektra" },  // Powers/Player/Elektra/Stealth.prototype
        {  13660u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/LukeCageChunkOConcreteMissileEffect.prototype
        {  13662u, "Doctor Strange" },  // Powers/Player/DoctorStrange/BasicDaggersMissileEffect.prototype
        {  13665u, "Daredevil" },  // Powers/Player/Daredevil/ShadowStrikeReappear.prototype
        {  13666u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveIronMan.prototype
        {  13667u, "Loki" },  // Powers/Player/Loki/UltimateIceShardEffect.prototype
        {  13672u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LokiIllusionRushCollideEffect.prototype
        {  13673u, "Cable" },  // Powers/Player/Cable/FutureBombPsimitar.prototype
        {  13674u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/AlterRealityHotspotEffect.prototype
        {  13675u, "Hawkeye" },  // Powers/Player/Hawkeye/DoubleShot.prototype
        {  13677u, "Iron Fist" },  // Powers/Player/IronFist/Talents/Talent3StanceComboMult.prototype
        {  13680u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceGainCombo60.prototype
        {  13683u, "Kitty Pryde" },  // Powers/Player/KittyPryde/TagTeamDamageBonusMult.prototype
        {  13684u, "Nova" },  // Powers/Player/Nova/PulsarHotspotSummonProcEffect.prototype
        {  13686u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotInfernoFireball.prototype
        {  13692u, "Ghost Rider" },  // Powers/Player/GhostRider/PassiveRegenCooldownDisplay.prototype
        {  13694u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/BouncingHexSecondChainEffect.prototype
        {  13697u, "Cyclops" },  // Powers/Player/Cyclops/CallAngelMovement.prototype
        {  13700u, "Black Widow" },  // Powers/Player/BlackWidow/WidowsBiteMissileEffect.prototype
        {  13702u, "Hawkeye" },  // Powers/Player/Hawkeye/NullifierArrowAreaHotspotProtectedEffect.prototype
        {  13703u, "Cyclops" },  // Powers/Player/Cyclops/FocusBeamPhase3Loop.prototype
        {  13705u, "Daredevil" },  // Powers/Player/Daredevil/Talents/OpenerCaneSlowTalent.prototype
        {  13707u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ChanneledBeamBuffPhase2Refresh.prototype
        {  13711u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/GiantGunGadgetEffect.prototype
        {  13712u, "Black Widow" },  // Powers/Player/BlackWidow/RoundhouseKick.prototype
        {  13713u, "Punisher" },  // Powers/Player/Punisher/Rework/Sidearms.prototype
        {  13718u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BasicBeam.prototype
        {  13721u, "Cable" },  // Powers/Player/Cable/PsychicBulletsPlus.prototype
        {  13727u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/Teleport.prototype
        {  13733u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/GadgetsAlwaysCrit.prototype
        {  13734u, "Thor" },  // Powers/Player/Thor/Talents/StackingOdinforceTalent.prototype
        {  13736u, "Storm" },  // Powers/Player/Storm/ChargedStrikeHiddenPassive.prototype
        {  13737u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/UltimateNoMore.prototype
        {  13738u, "Iron Man" },  // Powers/Player/IronMan/Signature.prototype
        {  13740u, "Black Bolt" },  // Powers/Player/BlackBolt/Traits/OffenseTrait.prototype
        {  13742u, "Carnage" },  // Powers/Player/Carnage/MegaClawHealthOnHitCombo.prototype
        {  13743u, "Loki" },  // Powers/Player/Loki/UltimateSummonBlizzardHotspotEff.prototype
        {  13744u, "Hulk" },  // Powers/Player/Hulk/Rework/GenerateMaxAngerCombo.prototype
        {  13746u, "Green Goblin" },  // Powers/Player/GreenGoblin/SonicToads.prototype
        {  13747u, "Angela" },  // Powers/Player/Angela/SigNoMatchBigHitCone.prototype
        {  13748u, "Punisher" },  // Powers/Player/Punisher/Rework/SMGTargetAudioCombo.prototype
        {  13750u, "Wolverine" },  // Powers/Player/Wolverine/BasicRonin.prototype
        {  13754u, "Blade" },  // Powers/Player/Blade/Talents/GlaiveCooldownTalent.prototype
        {  13756u, "Black Panther" },  // Powers/Player/BlackPanther/SummonDoraSecondComboLong.prototype
        {  13758u, "Black Panther" },  // Powers/Player/BlackPanther/MineFieldSummonDeployerArt.prototype
        {  13759u, "Wolverine" },  // Powers/Player/Wolverine/Rawr.prototype
        {  13761u, "Punisher" },  // Powers/Player/Punisher/Talents/GrenadeLauncher.prototype
        {  13763u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateCallIcemanAoA.prototype
        {  13766u, "Kitty Pryde" },  // Powers/Player/KittyPryde/UltimateHotspotSummon.prototype
        {  13771u, "Dr. Doom" },  // Powers/Player/DrDoom/BallLightningArcBigger.prototype
        {  13772u, "Black Cat" },  // Powers/Player/BlackCat/MasterThiefItemDrop.prototype
        {  13773u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TKTossJean.prototype
        {  13776u, "Magik" },  // Powers/Player/Magik/LifeTapHealEnduranceProcEffect.prototype
        {  13782u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/SpeedRushPhoenix.prototype
        {  13783u, "Loki" },  // Powers/Player/Loki/IllusionFromAboveDecoyPowerEnd.prototype
        {  13786u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceDecayCombo.prototype
        {  13789u, "Storm" },  // Powers/Player/Storm/Hailstorm.prototype
        {  13790u, "Nick Fury" },  // Powers/Player/NickFury/SummonShotgun.prototype
        {  13791u, "Elektra" },  // Powers/Player/Elektra/KillCommandSummonMystic.prototype
        {  13795u, "Nick Fury" },  // Powers/Player/NickFury/ShieldMedicProtectedBuff.prototype
        {  13796u, "Hawkeye" },  // Powers/Player/Hawkeye/KatanaEffectKnockdown.prototype
        {  13797u, "Loki" },  // Powers/Player/Loki/IceShardFreezeEffect.prototype
        {  13798u, "Nova" },  // Powers/Player/Nova/ChargedDashHotspotEffect.prototype
        {  13799u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GrootDefaultAttack.prototype
        {  13804u, "She-Hulk" },  // Powers/Player/SheHulk/MoveToStrikeConditionRemoval.prototype
        {  13811u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ToadTongueYank.prototype
        {  13813u, "Black Widow" },  // Powers/Player/BlackWidow/FlipKickComboEffect.prototype
        {  13816u, "Beast" },  // Powers/Player/Beast/Ultimate.prototype
        {  13818u, "Magik" },  // Powers/Player/Magik/BounceStrikeBounceMarvelNOW.prototype
        {  13820u, "Gambit" },  // Powers/Player/Gambit/AceOfSpadesMissileEffect.prototype
        {  13824u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexSphereChargeMechanicSR.prototype
        {  13825u, "Punisher" },  // Powers/Player/Punisher/Talents/HighCapacityMagazine.prototype
        {  13826u, "Black Widow" },  // Powers/Player/BlackWidow/RollingGrenades5.prototype
        {  13827u, "Beast" },  // Powers/Player/Beast/BrawnBoostMomentumRestoreCombo.prototype
        {  13828u, "Elektra" },  // Powers/Player/Elektra/Talents/NinjaMysticProc.prototype
        {  13829u, "Silver Surfer" },  // Powers/Player/SilverSurfer/DeathFromBelowComboEffect.prototype
        {  13831u, "Ghost Rider" },  // Powers/Player/GhostRider/Talents/Talent4OneFinalShow.prototype
        {  13832u, "Thor" },  // Powers/Player/Thor/Rework/UltimateGodBlastInvuln.prototype
        {  13836u, "Magik" },  // Powers/Player/Magik/Talents/Talent1LimboDemonIntoSpitter.prototype
        {  13837u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/MindCrush.prototype
        {  13839u, "X-23" },  // Powers/Player/X23/Eviscerate.prototype
        {  13841u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Talents/Talent3RifleBonuses.prototype
        {  13842u, "Ant-Man" },  // Powers/Player/AntMan/HealthOnShrinkHitProcEffect.prototype
        {  13844u, "Human Torch" },  // Powers/Player/HumanTorch/Traits/HeatCostCombo50.prototype
        {  13845u, "Juggernaut" },  // Powers/Player/Juggernaut/UltimateHotspotEffect.prototype
        {  13847u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent4MaxProjections.prototype
        {  13849u, "Blade" },  // Powers/Player/Blade/Talents/SpecLowRisk.prototype
        {  13852u, "Thing" },  // Powers/Player/Thing/Rework/FoodCartOrbSummon.prototype
        {  13854u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/Unique303MissileEffect.prototype
        {  13855u, "Deadpool" },  // Powers/Player/Deadpool/StinkBombHotspotEffect.prototype
        {  13858u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/MeleeSquirrelConeHitEffect.prototype
        {  13865u, "Juggernaut" },  // Powers/Player/Juggernaut/FullSpenderNextSpenderBuff.prototype
        {  13869u, "Black Widow" },  // Powers/Player/BlackWidow/Microdrones.prototype
        {  13870u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CosmicWakeHiddenPassive.prototype
        {  13871u, "Taskmaster" },  // Powers/Player/Taskmaster/SwordSlice4thHit.prototype
        {  13872u, "Black Cat" },  // Powers/Player/BlackCat/ClawPummel.prototype
        {  13874u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallJeanSummonObject.prototype
        {  13875u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BladeBloodlustHiddenPassive.prototype
        {  13877u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveAntManShrinkProc.prototype
        {  13879u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/ArcTurretPBAoE.prototype
        {  13881u, "Black Widow" },  // Powers/Player/BlackWidow/PlastiqueExplosion.prototype
        {  13883u, "Black Panther" },  // Powers/Player/BlackPanther/QuickSlashEnervationStackCombo.prototype
        {  13885u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LukeCagePummel1stAttack.prototype
        {  13886u, "Elektra" },  // Powers/Player/Elektra/Talents/StealthNoBreakTalent.prototype
        {  13887u, "Luke Cage" },  // Powers/Player/LukeCage/Talents/ComboPointsNoComboBar.prototype
        {  13894u, "Angela" },  // Powers/Player/Angela/DFACounter.prototype
        {  13895u, "Carnage" },  // Powers/Player/Carnage/Talents/BloodlustTracker.prototype
        {  13897u, "Iceman" },  // Powers/Player/Iceman/UltimateCloneRangedDefaultAttack.prototype
        {  13903u, "Ghost Rider" },  // Powers/Player/GhostRider/FlameScytheHotspotPassive.prototype
        {  13906u, "Winter Soldier" },  // Powers/Player/WinterSoldier/UltimateHiddenPassive.prototype
        {  13908u, "Carnage" },  // Powers/Player/Carnage/LungeDamage.prototype
        {  13911u, "Juggernaut" },  // Powers/Player/Juggernaut/ClotheslinePunchCollisionEffect.prototype
        {  13913u, "Wolverine" },  // Powers/Player/Wolverine/SignatureDashSlashHitFXLeft.prototype
        {  13914u, "Hulk" },  // Powers/Player/Hulk/UltimateStunCombo.prototype
        {  13916u, "Black Cat" },  // Powers/Player/BlackCat/GlueTrapExplosion.prototype
        {  13918u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveRedSkullProcEffectImpact.prototype
        {  13921u, "Juggernaut" },  // Powers/Player/Juggernaut/Traits/OffenseTraitStatConversionEffect.prototype
        {  13923u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceGainOverTimeCombo.prototype
        {  13924u, "Black Widow" },  // Powers/Player/BlackWidow/FlashGrenade.prototype
        {  13926u, "Nova" },  // Powers/Player/Nova/ExplosionFromMovementCounter.prototype
        {  13929u, "Loki" },  // Powers/Player/Loki/UltimateIceShard.prototype
        {  13930u, "Magik" },  // Powers/Player/Magik/LimboDemonLeapAttack.prototype
        {  13932u, "Deadpool" },  // Powers/Player/Deadpool/Rework/ServerLagGodModeEffect.prototype
        {  13933u, "Jean Grey" },  // Powers/Player/JeanGrey/PhoenixModeToggleRezHiddenPass.prototype
        {  13937u, "Green Goblin" },  // Powers/Player/GreenGoblin/Traits/OffenseTrait.prototype
        {  13938u, "Elektra" },  // Powers/Player/Elektra/KnifeRopeChainSummon.prototype
        {  13942u, "Wolverine" },  // Powers/Player/Wolverine/RunThroughSpenderVuln.prototype
        {  13945u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHoleProcEffect.prototype
        {  13947u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/FlametosserBossFireBreath.prototype
        {  13949u, "Human Torch" },  // Powers/Player/HumanTorch/Traits/MechanicTraitHeat.prototype
        {  13951u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/DoubleStrikeDamageFirstHitDamageCombo.prototype
        {  13952u, "Cable" },  // Powers/Player/Cable/VortexGrenade.prototype
        {  13954u, "Magneto" },  // Powers/Player/Magneto/BoomerangMetalMissileEffect.prototype
        {  13955u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ForceWallKnockback.prototype
        {  13957u, "Thor" },  // Powers/Player/Thor/Rework/SteroidStrong.prototype
        {  13959u, "Cyclops" },  // Powers/Player/Cyclops/DisengagingShotDodgeCombo.prototype
        {  13962u, "Vision" },  // Powers/Player/Vision/HeavySprintProcEffect.prototype
        {  13963u, "Deadpool" },  // Powers/Player/Deadpool/Rework/Omnislash.prototype
        {  13969u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GrootDeathProc.prototype
        {  13971u, "Carnage" },  // Powers/Player/Carnage/BasicClawsKnifeWasUsedLast.prototype
        {  13972u, "Ultron" },  // Powers/Player/Ultron/RangeDroneUnibeam.prototype
        {  13973u, "Magneto" },  // Powers/Player/Magneto/Traits/MechanicTraitDebris.prototype
        {  13974u, "Human Torch" },  // Powers/Player/HumanTorch/Traits/OverheatRemove.prototype
        {  13975u, "War Machine" },  // Powers/Player/WarMachine/TeslaFieldPBAOEEffect.prototype
        {  13977u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/ImplosionPhoenix.prototype
        {  13980u, "War Machine" },  // Powers/Player/WarMachine/LaserBladeDashProcEffect.prototype
        {  13981u, "Ultron" },  // Powers/Player/Ultron/FingerLasers.prototype
        {  13983u, "Ant-Man" },  // Powers/Player/AntMan/Talents/STSSBonusDamageTalent.prototype
        {  13984u, "Elektra" },  // Powers/Player/Elektra/EnergyHiddenPassive.prototype
        {  13985u, "Human Torch" },  // Powers/Player/HumanTorch/PassiveIgniteProc.prototype
        {  13986u, "Captain America" },  // Powers/Player/CaptainAmerica/Talents/ShieldBleeds.prototype
        {  13987u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/MovementPowerCDR.prototype
        {  13988u, "She-Hulk" },  // Powers/Player/SheHulk/BarristerBeatdown4thHit.prototype
        {  13989u, "Winter Soldier" },  // Powers/Player/WinterSoldier/SpinningMinesSummonLocusCombo.prototype
        {  13990u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Talents/ShadowmeldTalent.prototype
        {  13993u, "Doctor Strange" },  // Powers/Player/DoctorStrange/SeraphimShield.prototype
        {  13994u, "Cable" },  // Powers/Player/Cable/TKOverloadDamageShieldCombo.prototype
        {  13995u, "Storm" },  // Powers/Player/Storm/Talents/WindSpecDustDevilSummonProc.prototype
        {  13996u, "Carnage" },  // Powers/Player/Carnage/OrganicWebbing.prototype
        {  13997u, "Colossus" },  // Powers/Player/Colossus/NightcrawlerSummon/DefaultAttack.prototype
        {  14004u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BamfHotspotEffect.prototype
        {  14005u, "Gambit" },  // Powers/Player/Gambit/BoWhirlwindRandomCardRecurring.prototype
        {  14008u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/StealthToggleOutOfCombatDelayed.prototype
        {  14014u, "Punisher" },  // Powers/Player/Punisher/Traits/DefaultAmmoRegen.prototype
        {  14016u, "Blade" },  // Powers/Player/Blade/HelichopterComboSummon.prototype
        {  14019u, "Ultron" },  // Powers/Player/Ultron/FocusedConcussion.prototype
        {  14020u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/IronFistCraneStanceBuff.prototype
        {  14021u, "Beast" },  // Powers/Player/Beast/BeastDash.prototype
        {  14022u, "Wolverine" },  // Powers/Player/Wolverine/Talents/Talent1GreivousWoundsFuryBleedDmg.prototype
        {  14027u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/OnslaughtMentalOrbSelfKillStart.prototype
        {  14028u, "Ultron" },  // Powers/Player/Ultron/MeleeDroneBasicPunch2.prototype
        {  14030u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Talents/Talent3InfiltrationGear.prototype
        {  14031u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Traits/OffenseTrait.prototype
        {  14035u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateBattleVanFrontHotspotEffect.prototype
        {  14038u, "Magik" },  // Powers/Player/Magik/Talents/Talent3SteppingMastery.prototype
        {  14042u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/SignatureTKHurlPhoenixSummon.prototype
        {  14044u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/IronMaiden.prototype
        {  14047u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Traits/MechanicTraitLockheedEnergy.prototype
        {  14049u, "Ultron" },  // Powers/Player/Ultron/PowerRegenPassive.prototype
        {  14051u, "Colossus" },  // Powers/Player/Colossus/KittyPrydeSummon/PhaseAoEHotspotEffect.prototype
        {  14052u, "Psylocke" },  // Powers/Player/Psylocke/LungeNextAttackPsiGenerator.prototype
        {  14054u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/ColleenWing/ColleenWingDefaultAttack.prototype
        {  14057u, "Jean Grey" },  // Powers/Player/JeanGrey/DetectMindsSpiritGainProc.prototype
        {  14059u, "Punisher" },  // Powers/Player/Punisher/Rework/ReloadAutoComboEffect.prototype
        {  14060u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/StealthToggleConditionRemovalA.prototype
        {  14066u, "Loki" },  // Powers/Player/Loki/FrostArmorProcFreeze.prototype
        {  14067u, "She-Hulk" },  // Powers/Player/SheHulk/Talents/Talent2CeaseAndDesistOnHitObjection.prototype
        {  14068u, "Nova" },  // Powers/Player/Nova/BasicPunchStrafe.prototype
        {  14070u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DrainPhoenix.prototype
        {  14075u, "Blade" },  // Powers/Player/Blade/KnifeBarrage.prototype
        {  14077u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Signature.prototype
        {  14079u, "Psylocke" },  // Powers/Player/Psylocke/PsiEnergyGainOnNinjaOrPsionic.prototype
        {  14081u, "Nick Fury" },  // Powers/Player/NickFury/Talents/Talent2TumbleCharges.prototype
        {  14082u, "Ant-Man" },  // Powers/Player/AntMan/FlyingAntSwarm.prototype
        {  14087u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/SpendersAreFree.prototype
        {  14088u, "Black Cat" },  // Powers/Player/BlackCat/Assassinate2ndHit.prototype
        {  14089u, "Venom" },  // Powers/Player/Venom/UltimatePrepare.prototype
        {  14091u, "Wolverine" },  // Powers/Player/Wolverine/Talents/Talent4PBAoEDmgCDCrit.prototype
        {  14097u, "Loki" },  // Powers/Player/Loki/RefractingBurst.prototype
        {  14101u, "Cable" },  // Powers/Player/Cable/KineticRepulsionDamage.prototype
        {  14104u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent4Leaping.prototype
        {  14107u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedToggleFlyIn.prototype
        {  14108u, "Doctor Strange" },  // Powers/Player/DoctorStrange/EssenceOfZomHotspotEffect.prototype
        {  14112u, "Cable" },  // Powers/Player/Cable/EnergyPulsePlus.prototype
        {  14115u, "Nick Fury" },  // Powers/Player/NickFury/MicroDronesRandomPositionQuickerHit.prototype
        {  14116u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Traits/OffenseTrait.prototype
        {  14118u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/BouncingHexHiddenPassive.prototype
        {  14120u, "Nova" },  // Powers/Player/Nova/ExplosionFromMovementCombo.prototype
        {  14121u, "Loki" },  // Powers/Player/Loki/SwordSliceFireAoEProc.prototype
        {  14122u, "Nick Fury" },  // Powers/Player/NickFury/BasicPistolCombo1.prototype
        {  14123u, "Magneto" },  // Powers/Player/Magneto/ChanneledConeSlowHotspotEffect.prototype
        {  14127u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AmpControlledMob.prototype
        {  14128u, "Iron Fist" },  // Powers/Player/IronFist/ChiHarmonyAsCombo.prototype
        {  14132u, "Iceman" },  // Powers/Player/Iceman/FrostWedgeHotspotEffectVisual.prototype
        {  14136u, "Black Widow" },  // Powers/Player/BlackWidow/WidowmakerGainKillProc.prototype
        {  14139u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/RavenousBindingHealAoECombo.prototype
        {  14140u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateCycImplosionCombo.prototype
        {  14142u, "Moon Knight" },  // Powers/Player/MoonKnight/TributeGainMechanic.prototype
        {  14146u, "Ultron" },  // Powers/Player/Ultron/GroundThrowPhysicalBuff.prototype
        {  14147u, "X-23" },  // Powers/Player/X23/Pummel4.prototype
        {  14152u, "Rogue" },  // Powers/Player/Rogue/Talents/GlovesOffAuto.prototype
        {  14154u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Talents/Talent1BamfDiveBombStealthBuff.prototype
        {  14157u, "Black Panther" },  // Powers/Player/BlackPanther/FreezingDaggersMissileEffectBuffed.prototype
        {  14158u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/Drain.prototype
        {  14159u, "Black Panther" },  // Powers/Player/BlackPanther/AcrobaticAttackComboDamageComboExecute.prototype
        {  14160u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/DefaultAttack3.prototype
        {  14161u, "Black Panther" },  // Powers/Player/BlackPanther/FreezingDaggersSplashDamageBuffed.prototype
        {  14166u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedFireBreathHotspots.prototype
        {  14169u, "Black Panther" },  // Powers/Player/BlackPanther/SnareImmobilizeHotspotEffect.prototype
        {  14176u, "Magik" },  // Powers/Player/Magik/AssassinateCharges.prototype
        {  14177u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/OrbStorm.prototype
        {  14178u, "Wolverine" },  // Powers/Player/Wolverine/SignatureDashSlashHitFXRight.prototype
        {  14181u, "Storm" },  // Powers/Player/Storm/TornadoHotspotPassive.prototype
        {  14184u, "Rogue" },  // Powers/Player/Rogue/UppercutCraneStanceKickCombo.prototype
        {  14185u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveAntMan.prototype
        {  14186u, "Storm" },  // Powers/Player/Storm/ChargedStrikeEffect.prototype
        {  14190u, "Deadpool" },  // Powers/Player/Deadpool/Rework/BasicBleed.prototype
        {  14193u, "Captain America" },  // Powers/Player/CaptainAmerica/UltimateInvulnCombo.prototype
        {  14194u, "Beast" },  // Powers/Player/Beast/TeamworkSynergyEffect8s.prototype
        {  14197u, "Ghost Rider" },  // Powers/Player/GhostRider/FearCleanseStunCombo.prototype
        {  14198u, "Deadpool" },  // Powers/Player/Deadpool/UltimateBossCombo.prototype
        {  14201u, "Beast" },  // Powers/Player/Beast/Talents/Talent2PummelBrutChance.prototype
        {  14206u, "Cable" },  // Powers/Player/Cable/EnergyPulse.prototype
        {  14212u, "Cable" },  // Powers/Player/Cable/Traits/DefenseTrait.prototype
        {  14215u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshuSteroidSpiritVisual3.prototype
        {  14219u, "Vision" },  // Powers/Player/Vision/Traits/DefenseTraitUltraDense.prototype
        {  14222u, "Rogue" },  // Powers/Player/Rogue/RecallOverloadEndExplosion.prototype
        {  14224u, "Rogue" },  // Powers/Player/Rogue/DiveBombEnd.prototype
        {  14225u, "Angela" },  // Powers/Player/Angela/DisablingRibbonsBleedAsProc.prototype
        {  14226u, "Iceman" },  // Powers/Player/Iceman/Talents/BeamSpec.prototype
        {  14230u, "Taskmaster" },  // Powers/Player/Taskmaster/SwingingAssaultHasteCombo.prototype
        {  14232u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshuSteroidHealthVisual1.prototype
        {  14235u, "Winter Soldier" },  // Powers/Player/WinterSoldier/RapidFireReloadBuffCombo.prototype
        {  14238u, "Deadpool" },  // Powers/Player/Deadpool/Rework/OmnislashTeleport.prototype
        {  14253u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/MistyKnight/IceBeam.prototype
        {  14254u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent1MentalBuffDoTProc.prototype
        {  14259u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AreaDoTSlowVuln.prototype
        {  14260u, "Hawkeye" },  // Powers/Player/Hawkeye/Talents/TrickVolleyTalent.prototype
        {  14262u, "Thor" },  // Powers/Player/Thor/Rework/StormHammerThrow.prototype
        {  14267u, "Black Panther" },  // Powers/Player/BlackPanther/TripleShotConeDamage.prototype
        {  14268u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/UltimateBuffCombo.prototype
        {  14273u, "Juggernaut" },  // Powers/Player/Juggernaut/HandClapSmallDamageCombo.prototype
        {  14283u, "Ant-Man" },  // Powers/Player/AntMan/AntWallKnockbackDoTCombo.prototype
        {  14285u, "Ghost Rider" },  // Powers/Player/GhostRider/DFACritChanceLowHealthProc.prototype
        {  14287u, "Black Cat" },  // Powers/Player/BlackCat/Garrotte.prototype
        {  14290u, "Venom" },  // Powers/Player/Venom/Talents/DoubleSlashIchorSpearBuff.prototype
        {  14298u, "Wolverine" },  // Powers/Player/Wolverine/Traits/DefenseTrait.prototype
        {  14299u, "Magik" },  // Powers/Player/Magik/Talents/Talent4AutoBoneSpirit.prototype
        {  14301u, "Black Bolt" },  // Powers/Player/BlackBolt/MasterBlowPunchDamage.prototype
        {  14303u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveHydraAgentEnduranceProc.prototype
        {  14305u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/StormHailstormHotspotEffect.prototype
        {  14306u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/ShieldOfSeraphimTeamBuff.prototype
        {  14311u, "Rogue" },  // Powers/Player/Rogue/UltimateHiddenPassive.prototype
        {  14312u, "X-23" },  // Powers/Player/X23/BasicBloodyEnhanced.prototype
        {  14313u, "Nova" },  // Powers/Player/Nova/PulsarHotspotSpiritRestoration.prototype
        {  14314u, "Doctor Strange" },  // Powers/Player/DoctorStrange/MysticEnergyOrbVisual1.prototype
        {  14315u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotThumperCallInGroundPound.prototype
        {  14317u, "Cyclops" },  // Powers/Player/Cyclops/BeamBleed.prototype
        {  14318u, "Carnage" },  // Powers/Player/Carnage/BasicClawsAsCombo.prototype
        {  14319u, "Gambit" },  // Powers/Player/Gambit/RainInPainAsCombo.prototype
        {  14321u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallAngelEffect3.prototype
        {  14325u, "Cable" },  // Powers/Player/Cable/PsimitarLungeEffectNoDoT.prototype
        {  14327u, "Psylocke" },  // Powers/Player/Psylocke/PassiveDecoysSummonProc.prototype
        {  14330u, "Storm" },  // Powers/Player/Storm/Talents/TyphoonPull.prototype
        {  14331u, "Ultron" },  // Powers/Player/Ultron/DroneVulnerabilityCombo.prototype
        {  14336u, "Loki" },  // Powers/Player/Loki/MainSpecRangedBuffIce.prototype
        {  14338u, "Green Goblin" },  // Powers/Player/GreenGoblin/ExplosivePumpkinBase.prototype
        {  14340u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/SpikedBallChanneled.prototype
        {  14345u, "Loki" },  // Powers/Player/Loki/ChainBolt.prototype
        {  14346u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzyRangedFinisherMissileEffect.prototype
        {  14350u, "Iceman" },  // Powers/Player/Iceman/UltimateSummonClone.prototype
        {  14354u, "Luke Cage" },  // Powers/Player/LukeCage/GoodAtCombosCritRatingBuff.prototype
        {  14355u, "Magik" },  // Powers/Player/Magik/Talents/Talent5ReviveEnslavedMinionBuff.prototype
        {  14357u, "Iron Man" },  // Powers/Player/IronMan/OverheatSelfDamage.prototype
        {  14358u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/DiamondHeartBonus.prototype
        {  14359u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ArmyFromNothingMoreTargets.prototype
        {  14361u, "Venom" },  // Powers/Player/Venom/WrithingTendrilsWeakenCombo.prototype
        {  14362u, "Carnage" },  // Powers/Player/Carnage/ExcessTalentsIncrement1.prototype
        {  14364u, "Carnage" },  // Powers/Player/Carnage/Traits/SymbioteArmorDamageAbsorbStopper.prototype
        {  14366u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SuperSkrullWhirlwindHotspotEffect.prototype
        {  14367u, "Winter Soldier" },  // Powers/Player/WinterSoldier/ArmSmashEnd.prototype
        {  14374u, "Green Goblin" },  // Powers/Player/GreenGoblin/Talents/SignatureRestoreTalent.prototype
        {  14375u, "Hulk" },  // Powers/Player/Hulk/Traits/AngerDecayPreventionProc.prototype
        {  14376u, "Magneto" },  // Powers/Player/Magneto/AllInPickupRangeCombo.prototype
        {  14377u, "Venom" },  // Powers/Player/Venom/PBAoEBlob.prototype
        {  14382u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HumanTorchNovaBurst.prototype
        {  14390u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/DefenseHotspotNoChannel.prototype
        {  14392u, "Ghost Rider" },  // Powers/Player/GhostRider/PenanceStare.prototype
        {  14394u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/BouncingHex.prototype
        {  14396u, "Dr. Doom" },  // Powers/Player/DrDoom/BallLightningArc.prototype
        {  14397u, "Cyclops" },  // Powers/Player/Cyclops/Rework/SignatureBeamEffect.prototype
        {  14398u, "Captain America" },  // Powers/Player/CaptainAmerica/SoundRicochetMissileEffect.prototype
        {  14399u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PullUnderStart.prototype
        {  14403u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DamageMaelstromJeanHotspotEffect.prototype
        {  14410u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveDocOc.prototype
        {  14412u, "Nightcrawler" },  // Powers/Player/Nightcrawler/PassiveTeleportBuffTeamBuffCombo.prototype
        {  14415u, "Black Panther" },  // Powers/Player/BlackPanther/UltimateInvulnerableCombo.prototype
        {  14416u, "Angela" },  // Powers/Player/Angela/DisablingRibbonsHealthGain.prototype
        {  14417u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Talents/SigCooldownReduction.prototype
        {  14421u, "Storm" },  // Powers/Player/Storm/Talents/MassiveLightningStrike.prototype
        {  14422u, "Colossus" },  // Powers/Player/Colossus/ArmorHiddenPassive.prototype
        {  14423u, "Iceman" },  // Powers/Player/Iceman/FrozenOrbMissileEffect.prototype
        {  14424u, "Psylocke" },  // Powers/Player/Psylocke/PsiBarrierStopRegenOnHitProc.prototype
        {  14426u, "Nick Fury" },  // Powers/Player/NickFury/Traits/DefenseTrait.prototype
        {  14427u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/GoForTheEyesHit4.prototype
        {  14428u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/UnlockPotentialExplosion.prototype
        {  14430u, "Cable" },  // Powers/Player/Cable/TelepathicIllusionCooldownCounter.prototype
        {  14431u, "Juggernaut" },  // Powers/Player/Juggernaut/HandClap.prototype
        {  14433u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/WhirlwindHotspotEffect.prototype
        {  14434u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GrootOutSaplingSummon.prototype
        {  14435u, "Luke Cage" },  // Powers/Player/LukeCage/SummonIronFistCombo.prototype
        {  14436u, "Cyclops" },  // Powers/Player/Cyclops/Talents/BeamToPunchTalent.prototype
        {  14438u, "Ultron" },  // Powers/Player/Ultron/CleanseAlphaSpecLowHealthProc.prototype
        {  14445u, "Moon Knight" },  // Powers/Player/MoonKnight/CrescentDartFan.prototype
        {  14446u, "Cyclops" },  // Powers/Player/Cyclops/ChanneledEnergyBeamEffectUpgraded.prototype
        {  14448u, "Wolverine" },  // Powers/Player/Wolverine/EndRageRegen.prototype
        {  14454u, "Psylocke" },  // Powers/Player/Psylocke/SeekerButterfliesSelfCondition.prototype
        {  14455u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallMagnetoProc.prototype
        {  14456u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/X23CrimsonCircle.prototype
        {  14457u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Traits/MechanicTraitPowerCosmic.prototype
        {  14459u, "She-Hulk" },  // Powers/Player/SheHulk/ComboPointGainMechanic.prototype
        {  14462u, "Hawkeye" },  // Powers/Player/Hawkeye/ExplosiveQuiverEnabled.prototype
        {  14464u, "Beast" },  // Powers/Player/Beast/BeastDashBeastModeCombo.prototype
        {  14465u, "Doctor Strange" },  // Powers/Player/DoctorStrange/CrimsonBands.prototype
        {  14467u, "Magneto" },  // Powers/Player/Magneto/ElectromagneticShockwaveBuff.prototype
        {  14469u, "Jean Grey" },  // Powers/Player/JeanGrey/PhoenixModeToggleRezKnockbackEffect.prototype
        {  14471u, "Black Cat" },  // Powers/Player/BlackCat/TrapSignatureTrapExplosionHit.prototype
        {  14473u, "Iron Fist" },  // Powers/Player/IronFist/IronFistPunch.prototype
        {  14474u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCardMeleeStage3Damage.prototype
        {  14475u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Talents/Talent5PhaseOutDurationIncrease.prototype
        {  14476u, "Ant-Man" },  // Powers/Player/AntMan/Traits/DefenseTrait.prototype
        {  14478u, "Beast" },  // Powers/Player/Beast/CloseGapBleedComboStacking.prototype
        {  14479u, "Venom" },  // Powers/Player/Venom/SymbioteDrainPowerRed2.prototype
        {  14481u, "War Machine" },  // Powers/Player/WarMachine/AutogunMissileEffectPlasma.prototype
        {  14485u, "Iceman" },  // Powers/Player/Iceman/IcicleIcewallDeathAnimCancel.prototype
        {  14488u, "Iceman" },  // Powers/Player/Iceman/IceWallKnockback.prototype
        {  14489u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/BubbleSprayHotspotEffect.prototype
        {  14490u, "Nova" },  // Powers/Player/Nova/PulsarImplosion.prototype
        {  14493u, "Emma Frost" },  // Powers/Player/EmmaFrost/Traits/DiamondArmorRegenInCombatPause.prototype
        {  14495u, "Cyclops" },  // Powers/Player/Cyclops/Rework/ChargeConeSmallKnockdownCombo.prototype
        {  14497u, "Captain America" },  // Powers/Player/CaptainAmerica/ShieldBounceDestructibleKiller.prototype
        {  14500u, "Winter Soldier" },  // Powers/Player/WinterSoldier/ArmBlastKeywordConditionCombo.prototype
        {  14502u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/AngelDeathFromAboveCombo.prototype
        {  14505u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotBlockadeCallInAsCombo.prototype
        {  14507u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HulkSmash.prototype
        {  14512u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveCarnage.prototype
        {  14514u, "Colossus" },  // Powers/Player/Colossus/KittyPrydeSummon/DefaultAttack2.prototype
        {  14519u, "Black Cat" },  // Powers/Player/BlackCat/TrapDamageBonusToBasicWhip.prototype
        {  14520u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Unique315BoomerangMissileEffect.prototype
        {  14524u, "X-23" },  // Powers/Player/X23/Execute2ndHit.prototype
        {  14525u, "Juggernaut" },  // Powers/Player/Juggernaut/UnstoppableCharge.prototype
        {  14526u, "Colossus" },  // Powers/Player/Colossus/WolverineSummon/FlyingClawsBleed.prototype
        {  14530u, "Gambit" },  // Powers/Player/Gambit/RoyalFlushConeDamage.prototype
        {  14532u, "Black Bolt" },  // Powers/Player/BlackBolt/Talents/Talent5Energy100PctSteroid.prototype
        {  14533u, "Nick Fury" },  // Powers/Player/NickFury/WarmachinePlasmaCannon.prototype
        {  14535u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateBattleVanRearHotspotEffect.prototype
        {  14536u, "Cable" },  // Powers/Player/Cable/TelepathicIllusionAttackCombo.prototype
        {  14538u, "Taskmaster" },  // Powers/Player/Taskmaster/FreezeArrow.prototype
        {  14539u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/AutoShieldProcEffect.prototype
        {  14542u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/BasicRangedSquirrel.prototype
        {  14544u, "Nightcrawler" },  // Powers/Player/Nightcrawler/RighteousFrenzyDodgeChanceBuffCombo.prototype
        {  14546u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/ChargeBeamDmgBuffCombo.prototype
        {  14548u, "Nova" },  // Powers/Player/Nova/PulsarPassiveInvulnerability.prototype
        {  14550u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/JessicaJonesThrowConcrete.prototype
        {  14551u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/IronFistDragonStanceBuff.prototype
        {  14553u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Talents/Talent2TargetDispatched.prototype
        {  14554u, "Nova" },  // Powers/Player/Nova/SignatureSupernovaComboExplosion.prototype
        {  14557u, "Wolverine" },  // Powers/Player/Wolverine/SliceNDice.prototype
        {  14558u, "Magik" },  // Powers/Player/Magik/SoulCapture.prototype
        {  14559u, "Magneto" },  // Powers/Player/Magneto/DebrisCrush.prototype
        {  14561u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/SpikedBallChanneled.prototype
        {  14562u, "She-Hulk" },  // Powers/Player/SheHulk/BarristerBeatdown9thHit.prototype
        {  14574u, "Carnage" },  // Powers/Player/Carnage/Talents/ClawWeaponsHealing.prototype
        {  14575u, "Vision" },  // Powers/Player/Vision/HealingNanites.prototype
        {  14577u, "Rogue" },  // Powers/Player/Rogue/UppercutDragonStanceKickCombo.prototype
        {  14578u, "Magik" },  // Powers/Player/Magik/BloodSpirit.prototype
        {  14581u, "Winter Soldier" },  // Powers/Player/WinterSoldier/FuriousLunge.prototype
        {  14583u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/CombatTacticsProcEffect.prototype
        {  14588u, "Iceman" },  // Powers/Player/Iceman/RapidFire.prototype
        {  14589u, "Colossus" },  // Powers/Player/Colossus/ShockwaveTremors.prototype
        {  14590u, "Daredevil" },  // Powers/Player/Daredevil/Update/CaneAttack.prototype
        {  14591u, "Deadpool" },  // Powers/Player/Deadpool/Talents/StrafeExtraDefenseTalent.prototype
        {  14592u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoSummonPower.prototype
        {  14594u, "Taskmaster" },  // Powers/Player/Taskmaster/StudentsInvulnWhileSpawning.prototype
        {  14596u, "Dr. Doom" },  // Powers/Player/DrDoom/Talents/Talent3RobotSummonBonus.prototype
        {  14597u, "Hawkeye" },  // Powers/Player/Hawkeye/FreezeArrow.prototype
        {  14598u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/EnergyTurretShotMissileEffect.prototype
        {  14600u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GrootRegenerationPassive.prototype
        {  14603u, "Elektra" },  // Powers/Player/Elektra/KillCommandMysticArcaneBeam2.prototype
        {  14605u, "Cyclops" },  // Powers/Player/Cyclops/Talents/TeamSteroidGroupBuffsTalent.prototype
        {  14607u, "Emma Frost" },  // Powers/Player/EmmaFrost/TelepathyActive.prototype
        {  14608u, "Angela" },  // Powers/Player/Angela/SigNoMatchMovementEnd.prototype
        {  14611u, "Colossus" },  // Powers/Player/Colossus/SummonMagik.prototype
        {  14612u, "Deadpool" },  // Powers/Player/Deadpool/Rework/Strafe.prototype
        {  14614u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/PancakeKeywordConditionCombo.prototype
        {  14617u, "Winter Soldier" },  // Powers/Player/WinterSoldier/TripleShot.prototype
        {  14620u, "Rogue" },  // Powers/Player/Rogue/UltimateSignatureBamfPBAoE.prototype
        {  14623u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ForceDash.prototype
        {  14625u, "Iceman" },  // Powers/Player/Iceman/ShatterDamage.prototype
        {  14626u, "Ant-Man" },  // Powers/Player/AntMan/Talents/RedhotsStunTalent.prototype
        {  14628u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondSweepKick.prototype
        {  14630u, "Magik" },  // Powers/Player/Magik/LifeTapDoTAppliedByOtherPowerProc.prototype
        {  14631u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/WolverineBasicRoninBuff.prototype
        {  14634u, "Venom" },  // Powers/Player/Venom/SwingingAssault.prototype
        {  14636u, "Green Goblin" },  // Powers/Player/GreenGoblin/GoblinLaserBuff.prototype
        {  14639u, "Venom" },  // Powers/Player/Venom/ConeDrain.prototype
        {  14643u, "Green Goblin" },  // Powers/Player/GreenGoblin/PbAoESuperSpin.prototype
        {  14648u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/BasicPistols.prototype
        {  14650u, "Luke Cage" },  // Powers/Player/LukeCage/Pummel5thAttack.prototype
        {  14652u, "X-23" },  // Powers/Player/X23/Talents/Talent4SigChargeIncrease.prototype
        {  14657u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ImplodeExplodeVisualLockout.prototype
        {  14658u, "Loki" },  // Powers/Player/Loki/DecoyIllusionCleanseCombo.prototype
        {  14663u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveTombstone.prototype
        {  14664u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ArmyFromNothing.prototype
        {  14667u, "X-23" },  // Powers/Player/X23/Talents/Talent5StealthInvisRapidHealing.prototype
        {  14670u, "Psylocke" },  // Powers/Player/Psylocke/Traits/BarrierRegenPauseTrigger.prototype
        {  14672u, "Cable" },  // Powers/Player/Cable/PsychicBulletsMissileEffectPlus.prototype
        {  14673u, "Thing" },  // Powers/Player/Thing/Rework/RockslideChargeProcEffect.prototype
        {  14674u, "Ultron" },  // Powers/Player/Ultron/Traits/DefenseTrait.prototype
        {  14676u, "Nightcrawler" },  // Powers/Player/Nightcrawler/PassiveSwordsBleedEffect.prototype
        {  14677u, "Black Panther" },  // Powers/Player/BlackPanther/DoublePunch.prototype
        {  14678u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/UltimateTransformComboActivate.prototype
        {  14679u, "Human Torch" },  // Powers/Player/HumanTorch/NovaBurstVisual.prototype
        {  14684u, "Psylocke" },  // Powers/Player/Psylocke/PsiBarrierRegenHiddenPassive.prototype
        {  14693u, "Deadpool" },  // Powers/Player/Deadpool/Rework/PowerUpsRework.prototype
        {  14697u, "X-23" },  // Powers/Player/X23/TumbleEnd.prototype
        {  14698u, "Beast" },  // Powers/Player/Beast/PummelDamageCombo.prototype
        {  14704u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/AcornMeteorBonus.prototype
        {  14709u, "Doctor Strange" },  // Powers/Player/DoctorStrange/SevenRingsCombo.prototype
        {  14710u, "Thing" },  // Powers/Player/Thing/Rework/AuraPowerComboExclusive.prototype
        {  14714u, "Venom" },  // Powers/Player/Venom/DoubleSlashIchorGain.prototype
        {  14715u, "Ultron" },  // Powers/Player/Ultron/CleanseHealthOnHit.prototype
        {  14716u, "Cable" },  // Powers/Player/Cable/Talents/PsychicHazeLayer.prototype
        {  14718u, "Moon Knight" },  // Powers/Player/MoonKnight/NunchuckBulldozeComboSummon.prototype
        {  14719u, "Green Goblin" },  // Powers/Player/GreenGoblin/GliderUppercut.prototype
        {  14728u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/CleanseCCImmuneCombo.prototype
        {  14730u, "Juggernaut" },  // Powers/Player/Juggernaut/HandClapLargeDamageCombo.prototype
        {  14731u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/NeuralNetworkTransferPhoenixAOEProc.prototype
        {  14733u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/MysticismNoResetEffect.prototype
        {  14734u, "Captain America" },  // Powers/Player/CaptainAmerica/SerumHasPips.prototype
        {  14739u, "Beast" },  // Powers/Player/Beast/StompSmallEnd.prototype
        {  14740u, "Human Torch" },  // Powers/Player/HumanTorch/Traits/TorchStandingInHotspots.prototype
        {  14741u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveKurseSelfRez.prototype
        {  14742u, "Black Widow" },  // Powers/Player/BlackWidow/Traits/ChargeRegenTrigger.prototype
        {  14747u, "Black Cat" },  // Powers/Player/BlackCat/ClawPummel3.prototype
        {  14748u, "Silver Surfer" },  // Powers/Player/SilverSurfer/DeconstructionAutoTimer.prototype
        {  14752u, "Iron Fist" },  // Powers/Player/IronFist/CraneStance.prototype
        {  14755u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/HybridFormsSpec.prototype
        {  14757u, "Nova" },  // Powers/Player/Nova/PulsarImplosionEffectRR.prototype
        {  14759u, "Luke Cage" },  // Powers/Player/LukeCage/Pummel3rdAttack.prototype
        {  14760u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Traits/OffenseTrait.prototype
        {  14762u, "Thor" },  // Powers/Player/Thor/Rework/RagnarokOuterDamageCombo.prototype
        {  14764u, "She-Hulk" },  // Powers/Player/SheHulk/UltimatePillarSpin.prototype
        {  14767u, "Thing" },  // Powers/Player/Thing/CallStretchConditionDisable.prototype
        {  14768u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseDashBleed.prototype
        {  14770u, "Human Torch" },  // Powers/Player/HumanTorch/UltimateRemoveFireballs.prototype
        {  14771u, "Thing" },  // Powers/Player/Thing/Talents/Talent2HotHeadBuff.prototype
        {  14777u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BladeBloodlustLifeOnHit.prototype
        {  14779u, "Moon Knight" },  // Powers/Player/MoonKnight/CestusGauntletPunch.prototype
        {  14780u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/SpiritBoltMissileEffect.prototype
        {  14785u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/SignatureTKHurlPhoenix.prototype
        {  14786u, "Deadpool" },  // Powers/Player/Deadpool/Rework/CaltropsSelfDestruct.prototype
        {  14787u, "Hulk" },  // Powers/Player/Hulk/Rework/CarFistsProcEffect.prototype
        {  14792u, "Psylocke" },  // Powers/Player/Psylocke/AoEDoTSlowCombo.prototype
        {  14794u, "Elektra" },  // Powers/Player/Elektra/TripleChain2ndHit.prototype
        {  14796u, "Dr. Doom" },  // Powers/Player/DrDoom/ServoGuardRangedAttack.prototype
        {  14808u, "Wolverine" },  // Powers/Player/Wolverine/EndRageRegenProcEffect.prototype
        {  14810u, "Loki" },  // Powers/Player/Loki/Talents/FourRealmsColdFront.prototype
        {  14811u, "Thing" },  // Powers/Player/Thing/Rework/WiseCrackSpiritProcEffect.prototype
        {  14814u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/CircularLogic.prototype
        {  14815u, "Iron Fist" },  // Powers/Player/IronFist/SevenSidedStrikeHitLeopard.prototype
        {  14816u, "Psylocke" },  // Powers/Player/Psylocke/KatanaDoubleStrikeMissileEffectMental.prototype
        {  14822u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GrootOutSaplingAttackCombo.prototype
        {  14827u, "Wolverine" },  // Powers/Player/Wolverine/ReviveInvulnerabilityCombo.prototype
        {  14828u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/BouncingHexSecondChainEffectWiccan.prototype
        {  14835u, "Moon Knight" },  // Powers/Player/MoonKnight/ExplosiveCrescentDart.prototype
        {  14837u, "Taskmaster" },  // Powers/Player/Taskmaster/ComboPointGainMechanic.prototype
        {  14841u, "Iron Fist" },  // Powers/Player/IronFist/LeopardSlashStance.prototype
        {  14842u, "Winter Soldier" },  // Powers/Player/WinterSoldier/GrenadeLauncher.prototype
        {  14844u, "Moon Knight" },  // Powers/Player/MoonKnight/ConeYankStunCombo.prototype
        {  14846u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ForceDashMissileAbsorbCombo.prototype
        {  14849u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/InvisibilityDamageMovementSpeedBuff.prototype
        {  14851u, "Psylocke" },  // Powers/Player/Psylocke/PsiKatanaConeRanged.prototype
        {  14855u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ArmyFromNothingIntervalEffectMoreVuln.prototype
        {  14860u, "Emma Frost" },  // Powers/Player/EmmaFrost/Traits/DiamondArmorRegenMentalForm.prototype
        {  14861u, "Ant-Man" },  // Powers/Player/AntMan/InsectDecoyExplode.prototype
        {  14865u, "Cable" },  // Powers/Player/Cable/KineticBarrierSlowEffect.prototype
        {  14866u, "Dr. Doom" },  // Powers/Player/DrDoom/Talents/Talent5AllPower.prototype
        {  14869u, "Thor" },  // Powers/Player/Thor/Talents/MjolnirThrowTalent.prototype
        {  14870u, "Ultron" },  // Powers/Player/Ultron/DroneStrafe.prototype
        {  14871u, "Magik" },  // Powers/Player/Magik/UltimateHotspotEffect.prototype
        {  14874u, "Black Bolt" },  // Powers/Player/BlackBolt/PummelDamageShieldCombo.prototype
        {  14875u, "Iron Man" },  // Powers/Player/IronMan/UltimateSuitFreonRayHotspotFreez.prototype
        {  14878u, "Magik" },  // Powers/Player/Magik/NastirhMagicBomb.prototype
        {  14880u, "Elektra" },  // Powers/Player/Elektra/MarkForDeathAsProc.prototype
        {  14882u, "Magik" },  // Powers/Player/Magik/SoulswordBasicSpiritGain.prototype
        {  14884u, "Iron Fist" },  // Powers/Player/IronFist/ChiSteroidTigerClawStanceBuff.prototype
        {  14885u, "Thor" },  // Powers/Player/Thor/Rework/UltimateGodBlastAreaEffect.prototype
        {  14887u, "Black Widow" },  // Powers/Player/BlackWidow/UltimateAirstrikeShakeVisual.prototype
        {  14896u, "Iron Man" },  // Powers/Player/IronMan/Talents/UpgradeMicrolaser.prototype
        {  14897u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/QuicksilverSuperSonicCyclone.prototype
        {  14899u, "Thor" },  // Powers/Player/Thor/Rework/BasicRangedComboMissilePower.prototype
        {  14900u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MoleManMoloidLeaperMelee.prototype
        {  14902u, "Gambit" },  // Powers/Player/Gambit/StreetSweepMissileEffect.prototype
        {  14903u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Talents/Talent1BionicallyChargedThrow.prototype
        {  14904u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AxeHeelDropBuffEffect.prototype
        {  14908u, "Ghost Rider" },  // Powers/Player/GhostRider/InfernalSkullMissileEffect.prototype
        {  14912u, "Loki" },  // Powers/Player/Loki/Incinerate.prototype
        {  14915u, "Luke Cage" },  // Powers/Player/LukeCage/SummonColleenWingCombo.prototype
        {  14916u, "Gambit" },  // Powers/Player/Gambit/BoVault.prototype
        {  14919u, "War Machine" },  // Powers/Player/WarMachine/WarheadHotspotEffect.prototype
        {  14920u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SilverSurferChanneledBeamPhase3Loop.prototype
        {  14922u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/ResoundingWavesHotspotEffect.prototype
        {  14924u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/StarlordFireMissileEffect.prototype
        {  14927u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadlyBarrage.prototype
        {  14928u, "Hawkeye" },  // Powers/Player/Hawkeye/TaserArrowMissileEffect.prototype
        {  14932u, "Emma Frost" },  // Powers/Player/EmmaFrost/Traits/OffenseTrait.prototype
        {  14933u, "Colossus" },  // Powers/Player/Colossus/CallKittyAoE.prototype
        {  14936u, "Rogue" },  // Powers/Player/Rogue/Traits/StolenPassivePowerSlot2.prototype
        {  14942u, "Psylocke" },  // Powers/Player/Psylocke/SeekerButterfliesHealCombo.prototype
        {  14944u, "Winter Soldier" },  // Powers/Player/WinterSoldier/TeamStealthBuffEffect.prototype
        {  14946u, "Dr. Doom" },  // Powers/Player/DrDoom/FootDiveDamageNegationCombo.prototype
        {  14949u, "Hulk" },  // Powers/Player/Hulk/Rework/ShockwaveMissileEffectLarge.prototype
        {  14952u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveWarMachineProcEffect.prototype
        {  14953u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AmpControlledMobTargetControlledMob.prototype
        {  14954u, "Nick Fury" },  // Powers/Player/NickFury/RapidFireMissileEffect.prototype
        {  14961u, "Angela" },  // Powers/Player/Angela/Traits/HevensWrathPowerCostModifierCondition.prototype
        {  14962u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DarkPhoenixSpecHybridJeanBuff.prototype
        {  14964u, "Moon Knight" },  // Powers/Player/MoonKnight/CrescentDartFanDoT.prototype
        {  14965u, "Blade" },  // Powers/Player/Blade/SwordDash.prototype
        {  14972u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveWinterSoldier.prototype
        {  14973u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/FullAmmoRestoreCombo.prototype
        {  14974u, "Carnage" },  // Powers/Player/Carnage/Talents/SavageRebirth.prototype
        {  14976u, "Iron Fist" },  // Powers/Player/IronFist/UltimateBuffComboEffect.prototype
        {  14980u, "Cable" },  // Powers/Player/Cable/PsimitarImpaleKeywordConditionCombo.prototype
        {  14981u, "Punisher" },  // Powers/Player/Punisher/Rework/GrenadeLauncher.prototype
        {  14986u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceConditionRemoval.prototype
        {  14989u, "Loki" },  // Powers/Player/Loki/UltimateFrostNova.prototype
        {  14992u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Talents/Talent3SwordPowerTweaks.prototype
        {  14994u, "Carnage" },  // Powers/Player/Carnage/AxeDFA.prototype
        {  14996u, "Blade" },  // Powers/Player/Blade/BloodlustMaxedHeal.prototype
        {  14997u, "Rogue" },  // Powers/Player/Rogue/ExtremeDrain.prototype
        {  15001u, "Ghost Rider" },  // Powers/Player/GhostRider/ChargeUpBike.prototype
        {  15004u, "Captain America" },  // Powers/Player/CaptainAmerica/ShieldThrowPassiveBleedProcHP.prototype
        {  15007u, "Thor" },  // Powers/Player/Thor/Rework/ImmortalCombatCleanseCombo.prototype
        {  15012u, "Daredevil" },  // Powers/Player/Daredevil/Update/CaneAttackReflectAreaSummon.prototype
        {  15013u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateAvatarSwapPreventCombo.prototype
        {  15014u, "Kitty Pryde" },  // Powers/Player/KittyPryde/SwordPBAoE.prototype
        {  15017u, "Iceman" },  // Powers/Player/Iceman/Traits/OffenseTrait.prototype
        {  15018u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/UltimateHSEffect.prototype
        {  15020u, "Black Cat" },  // Powers/Player/BlackCat/ConeYankWhipDamageBonus.prototype
        {  15023u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/PsychicHammerDamageReductionCombo.prototype
        {  15028u, "Colossus" },  // Powers/Player/Colossus/WolverineSummon/FlyingClawsEnd.prototype
        {  15032u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Talents/Talent1FirearmStabilizer.prototype
        {  15035u, "Gambit" },  // Powers/Player/Gambit/BoWhirlwindKnockback.prototype
        {  15036u, "Luke Cage" },  // Powers/Player/LukeCage/TumbleKickMouseFollow.prototype
        {  15038u, "Thing" },  // Powers/Player/Thing/Talents/Talent3CallSuzieBuff.prototype
        {  15042u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosRiftHotspotVulnerability.prototype
        {  15045u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/CooldownSynergyRemoveCondition.prototype
        {  15053u, "Juggernaut" },  // Powers/Player/Juggernaut/Shockwave.prototype
        {  15058u, "Ant-Man" },  // Powers/Player/AntMan/Talents/OneTwoAntPunchTalent.prototype
        {  15059u, "Gambit" },  // Powers/Player/Gambit/Traits/MechanicTraitKineticEnergy.prototype
        {  15061u, "Luke Cage" },  // Powers/Player/LukeCage/PummelNoFinisher5thAttack.prototype
        {  15062u, "Taskmaster" },  // Powers/Player/Taskmaster/BasicShotTwo.prototype
        {  15065u, "Punisher" },  // Powers/Player/Punisher/Rework/Tumble.prototype
        {  15068u, "Carnage" },  // Powers/Player/Carnage/BasicClawsClawWasUsedLast.prototype
        {  15073u, "Wolverine" },  // Powers/Player/Wolverine/Talents/Talent5FeralRoarRapidRegen.prototype
        {  15075u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelAttackSquirrelPower.prototype
        {  15076u, "Dr. Doom" },  // Powers/Player/DrDoom/Repulsors.prototype
        {  15078u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SquirrelGirlSquirrelPetDefaultAt.prototype
        {  15079u, "Taskmaster" },  // Powers/Player/Taskmaster/StudentsPMCRiflemanRifleBurst.prototype
        {  15080u, "Rogue" },  // Powers/Player/Rogue/UltimateTransform.prototype
        {  15083u, "Colossus" },  // Powers/Player/Colossus/Traits/ArmorRegen.prototype
        {  15089u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotInfernoCallInAsCombo.prototype
        {  15096u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Ultimate.prototype
        {  15098u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/UnlockPotentialAsCombo.prototype
        {  15100u, "Punisher" },  // Powers/Player/Punisher/Talents/NuclearOption.prototype
        {  15102u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/IronManRepulsorBurstMissileEffect.prototype
        {  15103u, "Black Panther" },  // Powers/Player/BlackPanther/DoraMilajeDefaultAttackCombo.prototype
        {  15106u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Talents/Talent1HealingSpores.prototype
        {  15107u, "Daredevil" },  // Powers/Player/Daredevil/Update/EnergyHiddenPassive.prototype
        {  15108u, "Hawkeye" },  // Powers/Player/Hawkeye/Talents/PinningShotTalent.prototype
        {  15115u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateCallJeanSummonObject.prototype
        {  15116u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/X23CrimsonCircleDoTStack.prototype
        {  15117u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/CombatTactics.prototype
        {  15119u, "Ghost Rider" },  // Powers/Player/GhostRider/FirePillarPBAoEComboDoT.prototype
        {  15120u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/PsychicSpearMissileEffect.prototype
        {  15122u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/DefenseHotspotBuff.prototype
        {  15123u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/MassConfusion.prototype
        {  15130u, "Nova" },  // Powers/Player/Nova/NoPulsarHotspot.prototype
        {  15131u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/UltimateBubblestormSummonHotspot.prototype
        {  15134u, "Elektra" },  // Powers/Player/Elektra/Assassinate.prototype
        {  15140u, "Gambit" },  // Powers/Player/Gambit/StreetSweep.prototype
        {  15141u, "Blade" },  // Powers/Player/Blade/UVGrenadeTrigger.prototype
        {  15142u, "Beast" },  // Powers/Player/Beast/GlueBombDoTEffect.prototype
        {  15146u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/BasicMeleeEnduranceRestoreCombo.prototype
        {  15149u, "Moon Knight" },  // Powers/Player/MoonKnight/DeathFromAbove.prototype
        {  15150u, "Ultron" },  // Powers/Player/Ultron/SpinAttackHit1.prototype
        {  15152u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveNickFuryHiddenPassive.prototype
        {  15153u, "Deadpool" },  // Powers/Player/Deadpool/Rework/StrafeMissileEffectRandom.prototype
        {  15154u, "Captain America" },  // Powers/Player/CaptainAmerica/BoomerangThrowMissileEffect.prototype
        {  15156u, "Thing" },  // Powers/Player/Thing/Rework/WiseCrackBuffCombo.prototype
        {  15158u, "Deadpool" },  // Powers/Player/Deadpool/SummonHealthOrbCombo.prototype
        {  15164u, "Elektra" },  // Powers/Player/Elektra/BlowDartSummonCombo.prototype
        {  15165u, "Ultron" },  // Powers/Player/Ultron/BladeDroneLungeEffect.prototype
        {  15173u, "Jean Grey" },  // Powers/Player/JeanGrey/CarThrow.prototype
        {  15174u, "She-Hulk" },  // Powers/Player/SheHulk/AssaultBatteryBuff.prototype
        {  15176u, "Deadpool" },  // Powers/Player/Deadpool/SmellsLikeVictoryStackLossCondition.prototype
        {  15178u, "Juggernaut" },  // Powers/Player/Juggernaut/BonusMoveSpeedBasedOnMomentum.prototype
        {  15182u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/AutoAvatarOfCyttorak.prototype
        {  15188u, "Nova" },  // Powers/Player/Nova/Talents/Talent2MeleeBuffs.prototype
        {  15189u, "Thing" },  // Powers/Player/Thing/Rework/CallSuzieSummonCombo.prototype
        {  15192u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Ultimate.prototype
        {  15193u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/StormDisengagingShotMissileEffect.prototype
        {  15195u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BasicPunchEnduranceGain.prototype
        {  15196u, "Magneto" },  // Powers/Player/Magneto/SpawnMetalOrbProcOnHitMetalCage.prototype
        {  15199u, "Nova" },  // Powers/Player/Nova/PBAoENuke.prototype
        {  15200u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/ElektraMarkForDeath.prototype
        {  15201u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/DiamondHeartMentalComboStealth.prototype
        {  15205u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/CableKineticBarrier.prototype
        {  15207u, "Black Widow" },  // Powers/Player/BlackWidow/RollingGrenadesMissileEffect.prototype
        {  15210u, "Luke Cage" },  // Powers/Player/LukeCage/TumbleKickHitEffect.prototype
        {  15212u, "Venom" },  // Powers/Player/Venom/Talents/YankBuff.prototype
        {  15213u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/DragonSliceBuff.prototype
        {  15217u, "Storm" },  // Powers/Player/Storm/BallLightningBeam.prototype
        {  15220u, "Taskmaster" },  // Powers/Player/Taskmaster/ShieldBounceMissileEffect.prototype
        {  15225u, "Dr. Doom" },  // Powers/Player/DrDoom/GroundSmash.prototype
        {  15226u, "Black Bolt" },  // Powers/Player/BlackBolt/PummelDamageCombo.prototype
        {  15227u, "Ant-Man" },  // Powers/Player/AntMan/BioElectricBlastHit3.prototype
        {  15228u, "Cyclops" },  // Powers/Player/Cyclops/FocusBeamBuffPhase3Start.prototype
        {  15232u, "Nova" },  // Powers/Player/Nova/DeathFromAboveHotspotEffect.prototype
        {  15242u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent3PsychoBlast.prototype
        {  15243u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/DiveBombAIDirection.prototype
        {  15245u, "Ultron" },  // Powers/Player/Ultron/SignatureMeleePower.prototype
        {  15246u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ObfuscationSelfRez.prototype
        {  15252u, "Dr. Doom" },  // Powers/Player/DrDoom/FootDive.prototype
        {  15253u, "Iron Fist" },  // Powers/Player/IronFist/DragonStanceEnduranceMaterialOverride.prototype
        {  15254u, "Storm" },  // Powers/Player/Storm/LightningBoltDoT.prototype
        {  15259u, "Black Cat" },  // Powers/Player/BlackCat/BasicWhip.prototype
        {  15264u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/UltimateBubbleStormSRCoT.prototype
        {  15268u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/AngelDeathFromAboveBuffEffect.prototype
        {  15270u, "Ant-Man" },  // Powers/Player/AntMan/AntAllyCounterUp.prototype
        {  15272u, "Ghost Rider" },  // Powers/Player/GhostRider/BasicChainsHealCombo.prototype
        {  15273u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/DrDoomBallLightning.prototype
        {  15274u, "Beast" },  // Powers/Player/Beast/HulkingSlamHit.prototype
        {  15285u, "Jean Grey" },  // Powers/Player/JeanGrey/UltimateComboRankBoost.prototype
        {  15287u, "Angela" },  // Powers/Player/Angela/HybridTreeModIchor.prototype
        {  15288u, "Black Cat" },  // Powers/Player/BlackCat/WhipLash.prototype
        {  15289u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzyRangedMissileEffect.prototype
        {  15295u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveDaredevil.prototype
        {  15296u, "Blade" },  // Powers/Player/Blade/DeathFromAboveInnerVulnerableCombo.prototype
        {  15298u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Talents/Talent4FirearmsDmgBuff.prototype
        {  15301u, "Iceman" },  // Powers/Player/Iceman/IcemanClonesMeleeDefaultAttack.prototype
        {  15302u, "Moon Knight" },  // Powers/Player/MoonKnight/Traits/OffenseTrait.prototype
        {  15306u, "Green Goblin" },  // Powers/Player/GreenGoblin/SignatureMovementBuff.prototype
        {  15307u, "Juggernaut" },  // Powers/Player/Juggernaut/EarthquakeLeap.prototype
        {  15308u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent1HealthToArmorConversionInCombat.prototype
        {  15310u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveShieldAgent.prototype
        {  15312u, "Blade" },  // Powers/Player/Blade/KnifeBarrageEnduranceRestoreCombo.prototype
        {  15313u, "Punisher" },  // Powers/Player/Punisher/Rework/ReloadCooldownCombo.prototype
        {  15314u, "Wolverine" },  // Powers/Player/Wolverine/PBAoEBleedDoT.prototype
        {  15317u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/CleaSummonFlames.prototype
        {  15318u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseToggleOutOfCombatProc.prototype
        {  15319u, "X-23" },  // Powers/Player/X23/Talents/Talent3EviscerateMvmtSTSSFerocity.prototype
        {  15322u, "Beast" },  // Powers/Player/Beast/ElectroGadgetBuffTesla.prototype
        {  15324u, "Magik" },  // Powers/Player/Magik/LifeTap.prototype
        {  15328u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/PsychicHammerJean.prototype
        {  15331u, "Green Goblin" },  // Powers/Player/GreenGoblin/Talents/GoblinBlastExtraHitTalent.prototype
        {  15334u, "Black Bolt" },  // Powers/Player/BlackBolt/Pummel.prototype
        {  15335u, "Blade" },  // Powers/Player/Blade/Talents/ArsenalTalent.prototype
        {  15336u, "Thing" },  // Powers/Player/Thing/Talents/Talent3TauntBuff.prototype
        {  15341u, "Nightcrawler" },  // Powers/Player/Nightcrawler/TeleportBackstabHit.prototype
        {  15342u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateBeastMelee.prototype
        {  15345u, "Wolverine" },  // Powers/Player/Wolverine/TornadoClaw.prototype
        {  15346u, "Doctor Strange" },  // Powers/Player/DoctorStrange/BasicBoltsHiddenPassive.prototype
        {  15347u, "Beast" },  // Powers/Player/Beast/TumbleHotspotEffect.prototype
        {  15348u, "Dr. Doom" },  // Powers/Player/DrDoom/AoEDebuffFilterPower.prototype
        {  15354u, "Venom" },  // Powers/Player/Venom/SigFreakout.prototype
        {  15359u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallAngelOriginal.prototype
        {  15361u, "Black Widow" },  // Powers/Player/BlackWidow/UltimateCCImmunityCombo.prototype
        {  15364u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LivingLaserLaserBlastShieldCombo.prototype
        {  15366u, "Juggernaut" },  // Powers/Player/Juggernaut/Talents/UnstoppableChargeInfinite.prototype
        {  15367u, "Iceman" },  // Powers/Player/Iceman/ShatterBuffComboRapidFire.prototype
        {  15368u, "Thor" },  // Powers/Player/Thor/Rework/DeathFromAbovePhysicalDamageBuffCombo.prototype
        {  15369u, "Deadpool" },  // Powers/Player/Deadpool/QuickSlashBleedCombo.prototype
        {  15373u, "Rogue" },  // Powers/Player/Rogue/RecallOverloadHotspotEffectPhysical.prototype
        {  15375u, "War Machine" },  // Powers/Player/WarMachine/Talents/Talent4ReactiveShockArmor.prototype
        {  15381u, "Colossus" },  // Powers/Player/Colossus/TroyPunchDamageCombo.prototype
        {  15383u, "Daredevil" },  // Powers/Player/Daredevil/UltimateShadowStrikeMovement.prototype
        {  15385u, "Cyclops" },  // Powers/Player/Cyclops/Rework/BasicBeam.prototype
        {  15386u, "Angela" },  // Powers/Player/Angela/DeathFromAboveRibbonCombo.prototype
        {  15393u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshusFavorGainMechanic.prototype
        {  15394u, "Ant-Man" },  // Powers/Player/AntMan/RedHotsStun.prototype
        {  15398u, "Angela" },  // Powers/Player/Angela/Talents/SignatureAllRibbons.prototype
        {  15399u, "Rogue" },  // Powers/Player/Rogue/Talents/RapidPunchDashCharges.prototype
        {  15402u, "Iron Man" },  // Powers/Player/IronMan/Talents/Overclocked.prototype
        {  15403u, "Storm" },  // Powers/Player/Storm/Ultimate.prototype
        {  15404u, "Cyclops" },  // Powers/Player/Cyclops/Rework/RoundhouseVulnCombo.prototype
        {  15406u, "Cyclops" },  // Powers/Player/Cyclops/Talents/SigChannelTimeTalent.prototype
        {  15411u, "Magneto" },  // Powers/Player/Magneto/DebrisVisualPhase1.prototype
        {  15415u, "Deadpool" },  // Powers/Player/Deadpool/ExplosiveShotMSEffect.prototype
        {  15416u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Talents/Talent2H7FleetslayerBuffs.prototype
        {  15420u, "Juggernaut" },  // Powers/Player/Juggernaut/UnstoppableChargeShorterBuffCombo.prototype
        {  15424u, "Cable" },  // Powers/Player/Cable/TechnoOrganicVirusInvulnerability.prototype
        {  15426u, "Black Panther" },  // Powers/Player/BlackPanther/DoublePunchSecondHit.prototype
        {  15428u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TelepathicIllusionPhoenixDeathProcEffect.prototype
        {  15432u, "Punisher" },  // Powers/Player/Punisher/Rework/ChemicalBombExplosionEffect.prototype
        {  15433u, "Captain America" },  // Powers/Player/CaptainAmerica/TauntDmgAggroHiddenPassive.prototype
        {  15434u, "Gambit" },  // Powers/Player/Gambit/BatterUp.prototype
        {  15436u, "Ant-Man" },  // Powers/Player/AntMan/RapidShrinkStrike.prototype
        {  15438u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/MysticismDamagePulseMissile.prototype
        {  15440u, "Black Bolt" },  // Powers/Player/BlackBolt/SwoopingStrikesAreaAoEHit.prototype
        {  15441u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateBattleVanPartingShot.prototype
        {  15442u, "Black Panther" },  // Powers/Player/BlackPanther/DoublePunchAspdTooltip.prototype
        {  15443u, "Moon Knight" },  // Powers/Player/MoonKnight/SignatureFrenzyTributeGainFinisher.prototype
        {  15445u, "Hawkeye" },  // Powers/Player/Hawkeye/UltimateHotspotEffect.prototype
        {  15446u, "Storm" },  // Powers/Player/Storm/StormSurgeFreezingTempest.prototype
        {  15447u, "Kitty Pryde" },  // Powers/Player/KittyPryde/MovementSlashBleed.prototype
        {  15449u, "Luke Cage" },  // Powers/Player/LukeCage/PummelNoFinisher3rdAttack.prototype
        {  15450u, "Doctor Strange" },  // Powers/Player/DoctorStrange/WindsOfWatoombCCImmuneCombo.prototype
        {  15451u, "Thing" },  // Powers/Player/Thing/Rework/DiscusBleedCombo.prototype
        {  15455u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/RavenousBindingHealEffect.prototype
        {  15456u, "Punisher" },  // Powers/Player/Punisher/Traits/DefaultAmmoRegenEndAsCombo.prototype
        {  15458u, "Angela" },  // Powers/Player/Angela/SigNoMatchCounter.prototype
        {  15459u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/StormHailstormHotspotSlowEffect.prototype
        {  15460u, "Ultron" },  // Powers/Player/Ultron/Talents/Talent4PullTowards.prototype
        {  15461u, "Emma Frost" },  // Powers/Player/EmmaFrost/AmpControlledMobHotspotEffect.prototype
        {  15462u, "Loki" },  // Powers/Player/Loki/MeddlingStrike.prototype
        {  15463u, "Punisher" },  // Powers/Player/Punisher/Talents/ExplosiveRoundsProcEffect.prototype
        {  15465u, "Black Cat" },  // Powers/Player/BlackCat/Talents/TalentClawPummelBonus.prototype
        {  15469u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/IronFistStanceUppercutChargeRemoval.prototype
        {  15470u, "Iron Fist" },  // Powers/Player/IronFist/ChiBlast.prototype
        {  15471u, "Gambit" },  // Powers/Player/Gambit/UltimateRogueDefaultAttack.prototype
        {  15475u, "Carnage" },  // Powers/Player/Carnage/UltimateHotspotEffect.prototype
        {  15478u, "Moon Knight" },  // Powers/Player/MoonKnight/CestusGauntletPunchTributeGain.prototype
        {  15479u, "Venom" },  // Powers/Player/Venom/IchorVisualPassiveRemoval.prototype
        {  15480u, "Magneto" },  // Powers/Player/Magneto/Talents/AutoDebrisCrush.prototype
        {  15481u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateCallIcemanAllNewXmen.prototype
        {  15482u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Traits/DefenseTrait.prototype
        {  15486u, "Moon Knight" },  // Powers/Player/MoonKnight/UltimateHiddenPassive.prototype
        {  15489u, "Black Widow" },  // Powers/Player/BlackWidow/RollingGrenades2.prototype
        {  15490u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/BouncingFireballs.prototype
        {  15495u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceDelayDecayCombo.prototype
        {  15496u, "Blade" },  // Powers/Player/Blade/UVGrenadePulsingController.prototype
        {  15499u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HydeDirectedShockwavePBAOECombo.prototype
        {  15501u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BoardDashRechargeOnHit.prototype
        {  15509u, "Ant-Man" },  // Powers/Player/AntMan/AntnadoHotspot.prototype
        {  15511u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelRapidFireStackingBuff.prototype
        {  15512u, "Rogue" },  // Powers/Player/Rogue/Charge.prototype
        {  15515u, "Winter Soldier" },  // Powers/Player/WinterSoldier/TeamBuffConcealedWinterSoldier.prototype
        {  15516u, "Punisher" },  // Powers/Player/Punisher/Rework/Minigun.prototype
        {  15517u, "Luke Cage" },  // Powers/Player/LukeCage/Pummel.prototype
        {  15518u, "Hulk" },  // Powers/Player/Hulk/Rework/WorldbreakerStunCombo.prototype
        {  15521u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LukeCagePummel6thAttack.prototype
        {  15524u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/CapBoomerangShieldMissileEffect.prototype
        {  15525u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveThing.prototype
        {  15526u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/KneelBeforeMe.prototype
        {  15527u, "Punisher" },  // Powers/Player/Punisher/Rework/RPGMultiBarrel.prototype
        {  15530u, "Nightcrawler" },  // Powers/Player/Nightcrawler/ValiantLeapBounce.prototype
        {  15532u, "Green Goblin" },  // Powers/Player/GreenGoblin/TheBigOneLittleBombs.prototype
        {  15533u, "Winter Soldier" },  // Powers/Player/WinterSoldier/ArmBlastEnabled.prototype
        {  15535u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DarkPhoenixPhoenixBuff.prototype
        {  15537u, "Nightcrawler" },  // Powers/Player/Nightcrawler/SwordThrowMissileCombo.prototype
        {  15538u, "Magik" },  // Powers/Player/Magik/LimboSpitterDefaultAttack.prototype
        {  15540u, "Hulk" },  // Powers/Player/Hulk/Rework/ShockwaveMissileEffectSmall.prototype
        {  15542u, "Iron Man" },  // Powers/Player/IronMan/RemoveShieldCombo.prototype
        {  15543u, "Blade" },  // Powers/Player/Blade/JustStayDownBuff.prototype
        {  15544u, "Colossus" },  // Powers/Player/Colossus/SummonMagikHealPassive.prototype
        {  15545u, "Ant-Man" },  // Powers/Player/AntMan/AnthillPassive.prototype
        {  15546u, "Iceman" },  // Powers/Player/Iceman/FrostWedgeIntervalEffect.prototype
        {  15547u, "Thing" },  // Powers/Player/Thing/Rework/HeadbuttBleedCombo.prototype
        {  15554u, "Elektra" },  // Powers/Player/Elektra/AssassinateComboBoss.prototype
        {  15557u, "Gambit" },  // Powers/Player/Gambit/UltimateCardThrow6.prototype
        {  15558u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Traits/MysticismDamagePulseEffect.prototype
        {  15560u, "She-Hulk" },  // Powers/Player/SheHulk/BarExam.prototype
        {  15561u, "Venom" },  // Powers/Player/Venom/ConeTendrilsHotspotEffect.prototype
        {  15562u, "Ant-Man" },  // Powers/Player/AntMan/AntSpenderSpiritRestore8pct.prototype
        {  15563u, "Hulk" },  // Powers/Player/Hulk/Rework/ClapHandDamageCombo.prototype
        {  15566u, "Storm" },  // Powers/Player/Storm/StormSurgeWindTempest.prototype
        {  15567u, "Black Panther" },  // Powers/Player/BlackPanther/Talents/TripleThrowSpenderSpec.prototype
        {  15568u, "Loki" },  // Powers/Player/Loki/DecoyIllusionSummonCombo.prototype
        {  15571u, "Blade" },  // Powers/Player/Blade/HemoglycerinGauntlet1stAttack.prototype
        {  15572u, "Blade" },  // Powers/Player/Blade/UnleashGlaive.prototype
        {  15573u, "Angela" },  // Powers/Player/Angela/Talents/HevensWrathDamageBoost.prototype
        {  15574u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHoleSummonInstagibCombo.prototype
        {  15578u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallMagnetoSummon.prototype
        {  15579u, "Taskmaster" },  // Powers/Player/Taskmaster/FuriousLungeEffect.prototype
        {  15580u, "Black Widow" },  // Powers/Player/BlackWidow/Traits/DefenseTrait.prototype
        {  15582u, "Taskmaster" },  // Powers/Player/Taskmaster/ShieldBashEffect.prototype
        {  15583u, "Venom" },  // Powers/Player/Venom/SigFreakoutIchorGain.prototype
        {  15586u, "Nova" },  // Powers/Player/Nova/LungingPunchEffect.prototype
        {  15590u, "Doctor Strange" },  // Powers/Player/DoctorStrange/DemonsOfDenakHiddenPassive.prototype
        {  15591u, "Vision" },  // Powers/Player/Vision/Traits/DefenseTrait.prototype
        {  15592u, "Rogue" },  // Powers/Player/Rogue/RecallOverloadPrepareEndExplosionPhysical.prototype
        {  15593u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/PunchStingProc.prototype
        {  15594u, "Human Torch" },  // Powers/Player/HumanTorch/BowlingBall.prototype
        {  15597u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/BouncyBuild.prototype
        {  15598u, "Black Widow" },  // Powers/Player/BlackWidow/PunchComboEffect.prototype
        {  15599u, "Emma Frost" },  // Powers/Player/EmmaFrost/Traits/DiamondFormDeactivate.prototype
        {  15600u, "War Machine" },  // Powers/Player/WarMachine/EMPRobotDmg.prototype
        {  15603u, "Loki" },  // Powers/Player/Loki/LightPillarSpikeVulnerability.prototype
        {  15604u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexSphereOrbVisual3.prototype
        {  15605u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexSphereSummonBox.prototype
        {  15607u, "Black Bolt" },  // Powers/Player/BlackBolt/KillingWordNonSig.prototype
        {  15609u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/CooldownSynergyBuff.prototype
        {  15610u, "Gambit" },  // Powers/Player/Gambit/ChargeUpCardShowCard.prototype
        {  15611u, "Elektra" },  // Powers/Player/Elektra/CrossStrikeSummon.prototype
        {  15613u, "Gambit" },  // Powers/Player/Gambit/StreetSweepHotspotEffect.prototype
        {  15614u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MandarinElectricStorm.prototype
        {  15615u, "Deadpool" },  // Powers/Player/Deadpool/Talents/GodModeTalent.prototype
        {  15618u, "Hulk" },  // Powers/Player/Hulk/HandclapDeflectionEffect.prototype
        {  15619u, "Gambit" },  // Powers/Player/Gambit/TumbleCCImmunityCombo.prototype
        {  15621u, "Ghost Rider" },  // Powers/Player/GhostRider/FirePillar.prototype
        {  15623u, "Punisher" },  // Powers/Player/Punisher/Rework/IgnorePainUpdateBuffEffect2.prototype
        {  15625u, "Loki" },  // Powers/Player/Loki/UltimateTransformProcDeactivate.prototype
        {  15626u, "Magik" },  // Powers/Player/Magik/Talents/Talent2LifeTapConfuse.prototype
        {  15631u, "Elektra" },  // Powers/Player/Elektra/UltimateHiddenPassive.prototype
        {  15632u, "Ultron" },  // Powers/Player/Ultron/RapidFireRight.prototype
        {  15635u, "Magik" },  // Powers/Player/Magik/Traits/DefenseTrait.prototype
        {  15637u, "Luke Cage" },  // Powers/Player/LukeCage/SummonColleenWing.prototype
        {  15639u, "Vision" },  // Powers/Player/Vision/HeavySprint.prototype
        {  15641u, "Elektra" },  // Powers/Player/Elektra/RemoveNinjaMaster.prototype
        {  15642u, "Storm" },  // Powers/Player/Storm/ElementalStorm.prototype
        {  15646u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/GrootDeathFromAboveMovieVol2.prototype
        {  15650u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/AngelDeathFromAboveShowWingsVisualCondition.prototype
        {  15652u, "Ghost Rider" },  // Powers/Player/GhostRider/ContractSpecLowHealthBuffEffect.prototype
        {  15659u, "Black Bolt" },  // Powers/Player/BlackBolt/Talents/Talent2MovementBoost.prototype
        {  15660u, "Ghost Rider" },  // Powers/Player/GhostRider/FireBreathHotspotEffect.prototype
        {  15662u, "Deadpool" },  // Powers/Player/Deadpool/FourthWallCDDisplay.prototype
        {  15664u, "Storm" },  // Powers/Player/Storm/Talents/WindSpec.prototype
        {  15665u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/AoEFearMassConfusion.prototype
        {  15667u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/UltimateTransformImpact.prototype
        {  15668u, "Ghost Rider" },  // Powers/Player/GhostRider/DFAFirePillarSummonCombo.prototype
        {  15670u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosBlastComboFilter.prototype
        {  15671u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/StormHailstorm.prototype
        {  15672u, "Rogue" },  // Powers/Player/Rogue/Uppercut.prototype
        {  15674u, "Human Torch" },  // Powers/Player/HumanTorch/FlameTornadoCycloneSummonCombo.prototype
        {  15675u, "Black Cat" },  // Powers/Player/BlackCat/NineLivesHealthGainProc.prototype
        {  15681u, "Human Torch" },  // Powers/Player/HumanTorch/NovaBurst.prototype
        {  15682u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/CyclopsBeamMissileEffect.prototype
        {  15683u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/NeuralNetworkJean2ndTargetCombo.prototype
        {  15686u, "War Machine" },  // Powers/Player/WarMachine/AutogunMissilePower.prototype
        {  15687u, "Iron Man" },  // Powers/Player/IronMan/UltimateSuitBasicPunch1.prototype
        {  15688u, "Cyclops" },  // Powers/Player/Cyclops/OpticExplosionMissileEffect.prototype
        {  15689u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/HexBoltRestoreEffect.prototype
        {  15690u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Invisibility.prototype
        {  15691u, "Cyclops" },  // Powers/Player/Cyclops/TeamSteroidCooldownResetCombo.prototype
        {  15692u, "Iron Fist" },  // Powers/Player/IronFist/TigerClawAccuracyTeamBuff.prototype
        {  15693u, "Iron Fist" },  // Powers/Player/IronFist/StanceOnCooldown.prototype
        {  15695u, "Colossus" },  // Powers/Player/Colossus/TroyPunch.prototype
        {  15696u, "Captain America" },  // Powers/Player/CaptainAmerica/DeathFromAboveVulnerabilityCombo.prototype
        {  15699u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/ImplosionJeanPullCombo.prototype
        {  15700u, "Magneto" },  // Powers/Player/Magneto/MetalCage.prototype
        {  15701u, "Carnage" },  // Powers/Player/Carnage/TransfusionHealthOnHitCombo.prototype
        {  15702u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickQuiverComboTaserArrow.prototype
        {  15709u, "Nightcrawler" },  // Powers/Player/Nightcrawler/TeleportDamageBuffProc.prototype
        {  15712u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ManApeBeatChest.prototype
        {  15713u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/WaspBiospray.prototype
        {  15714u, "Iron Fist" },  // Powers/Player/IronFist/LeopardSlashSingleStanceBuff.prototype
        {  15718u, "Ant-Man" },  // Powers/Player/AntMan/NotSoBigPunchDamageCombo.prototype
        {  15720u, "Doctor Strange" },  // Powers/Player/DoctorStrange/ProjectionBasicBolts.prototype
        {  15724u, "Luke Cage" },  // Powers/Player/LukeCage/BasicCrowbar.prototype
        {  15727u, "Iron Man" },  // Powers/Player/IronMan/OverheatGainProc.prototype
        {  15728u, "Human Torch" },  // Powers/Player/HumanTorch/BasicFireWedgeHotspotEffect.prototype
        {  15732u, "Human Torch" },  // Powers/Player/HumanTorch/SummonFireHotspot.prototype
        {  15738u, "Storm" },  // Powers/Player/Storm/LightningBolt.prototype
        {  15741u, "Ultron" },  // Powers/Player/Ultron/Talents/Talent2RangedDrones.prototype
        {  15743u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/SniperNest.prototype
        {  15744u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AxeHeelDropSweepKickBuff.prototype
        {  15746u, "Magneto" },  // Powers/Player/Magneto/DebrisVIsualPhase2Removal.prototype
        {  15748u, "Nick Fury" },  // Powers/Player/NickFury/ShieldMedicAgentRifleAttack.prototype
        {  15758u, "Human Torch" },  // Powers/Player/HumanTorch/Traits/HeatRegen.prototype
        {  15760u, "Iceman" },  // Powers/Player/Iceman/UltimateStart.prototype
        {  15762u, "Moon Knight" },  // Powers/Player/MoonKnight/UltimateDamageEffect.prototype
        {  15764u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/StepfordUltimate.prototype
        {  15766u, "Magik" },  // Powers/Player/Magik/SoulCaptureControlAI.prototype
        {  15771u, "Wolverine" },  // Powers/Player/Wolverine/ReviveHiddenPassiveSelfRez.prototype
        {  15773u, "Magneto" },  // Powers/Player/Magneto/NegativePolarityProcEffect.prototype
        {  15774u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/AppliedBarrier.prototype
        {  15776u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Talents/Talent5FlourishBuff.prototype
        {  15781u, "Magik" },  // Powers/Player/Magik/AssassinateCombo.prototype
        {  15786u, "Jean Grey" },  // Powers/Player/JeanGrey/PanicJean.prototype
        {  15787u, "Black Bolt" },  // Powers/Player/BlackBolt/SwoopingStrikesAreaHit.prototype
        {  15791u, "Storm" },  // Powers/Player/Storm/SiroccoLunge.prototype
        {  15792u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/CrossbonesRangedDamageProc.prototype
        {  15793u, "Cable" },  // Powers/Player/Cable/VortexGrenadeSlowCombo.prototype
        {  15798u, "Thing" },  // Powers/Player/Thing/Traits/ColbberinTimeGainOnHotspotHit.prototype
        {  15799u, "Magneto" },  // Powers/Player/Magneto/Talents/DebrisGeneratorBuffProcEffect.prototype
        {  15800u, "Thor" },  // Powers/Player/Thor/Rework/OdinforcePermaEffect.prototype
        {  15802u, "She-Hulk" },  // Powers/Player/SheHulk/ConvictionEnd.prototype
        {  15803u, "Loki" },  // Powers/Player/Loki/UnveiledSummonHotspot.prototype
        {  15806u, "Cyclops" },  // Powers/Player/Cyclops/CallEmmaKneelBeforeMeComboExplosion.prototype
        {  15808u, "Cyclops" },  // Powers/Player/Cyclops/Rework/ConeBeam.prototype
        {  15809u, "Nova" },  // Powers/Player/Nova/BouncingStrike.prototype
        {  15810u, "Iron Man" },  // Powers/Player/IronMan/FreonRayVulnCombo.prototype
        {  15811u, "Nick Fury" },  // Powers/Player/NickFury/CallRedwing.prototype
        {  15813u, "Luke Cage" },  // Powers/Player/LukeCage/Talents/PowerBuffsLongRangeWeapons.prototype
        {  15814u, "Black Panther" },  // Powers/Player/BlackPanther/QuickSlash.prototype
        {  15816u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickQuiverComboFreezeArrowProc.prototype
        {  15818u, "Silver Surfer" },  // Powers/Player/SilverSurfer/DeconstructionAsProc.prototype
        {  15821u, "Taskmaster" },  // Powers/Player/Taskmaster/StudentsBuffOnSpawn.prototype
        {  15824u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ElectroElementalStormHotspotEffe.prototype
        {  15826u, "Hulk" },  // Powers/Player/Hulk/UltimateImplosionCombo.prototype
        {  15829u, "Green Goblin" },  // Powers/Player/GreenGoblin/GasPumpkinHotspotEffect.prototype
        {  15833u, "Hawkeye" },  // Powers/Player/Hawkeye/ShriekingArrowBurn.prototype
        {  15834u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Traits/OffenseTrait.prototype
        {  15838u, "Cyclops" },  // Powers/Player/Cyclops/Rework/Tumble.prototype
        {  15846u, "Cable" },  // Powers/Player/Cable/TKSpearSlam.prototype
        {  15851u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/RocketNukeMissileEffect.prototype
        {  15858u, "Nova" },  // Powers/Player/Nova/DeathFromAboveShieldGainCombo.prototype
        {  15863u, "Dr. Doom" },  // Powers/Player/DrDoom/RepulsorsSpiritGainComboEffect.prototype
        {  15865u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LokiIllusionRushDecoyPowerCollid.prototype
        {  15870u, "Thing" },  // Powers/Player/Thing/Rework/YancyStreetGang.prototype
        {  15871u, "Green Goblin" },  // Powers/Player/GreenGoblin/GhostBombMissileEffect.prototype
        {  15874u, "Moon Knight" },  // Powers/Player/MoonKnight/KhonshuSteroidHealthTauntAura.prototype
        {  15876u, "Cable" },  // Powers/Player/Cable/FutureBombTaunt.prototype
        {  15880u, "Daredevil" },  // Powers/Player/Daredevil/Update/BillyClubSweep.prototype
        {  15881u, "Beast" },  // Powers/Player/Beast/FlyingBeatdownHideMeshInvuln.prototype
        {  15882u, "Dr. Doom" },  // Powers/Player/DrDoom/DoomBotInfernoDeathExplosionEffect.prototype
        {  15883u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BrimstoneBlitzHotspotEffect.prototype
        {  15884u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Talents/Talent2SigChaosCosts.prototype
        {  15889u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/StretchyBrainHiddenPassive.prototype
        {  15890u, "Magik" },  // Powers/Player/Magik/LifeTapComboAmpDamageSpec.prototype
        {  15895u, "Daredevil" },  // Powers/Player/Daredevil/Update/OpeningLunge.prototype
        {  15897u, "Venom" },  // Powers/Player/Venom/SigFreakoutImplosionCombo.prototype
        {  15900u, "Colossus" },  // Powers/Player/Colossus/MagikSummon/MagikDefaultAttackCombo2.prototype
        {  15903u, "Ultron" },  // Powers/Player/Ultron/Dash.prototype
        {  15904u, "Black Bolt" },  // Powers/Player/BlackBolt/Talents/Talent2BeamRemap.prototype
        {  15906u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/PassiveShieldOrbPickupEffect.prototype
        {  15907u, "Loki" },  // Powers/Player/Loki/Unveiled.prototype
        {  15912u, "Green Goblin" },  // Powers/Player/GreenGoblin/HallucinogenicPumpkinEffect.prototype
        {  15916u, "Blade" },  // Powers/Player/Blade/Traits/DefenseTrait.prototype
        {  15917u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/BouncingHexChainEffect.prototype
        {  15925u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/DefaultAttack.prototype
        {  15926u, "War Machine" },  // Powers/Player/WarMachine/Traits/OffenseTraitStatConversionEffect.prototype
        {  15930u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/QuicksilverSuperSonicCycloneHotspotEffect.prototype
        {  15931u, "Carnage" },  // Powers/Player/Carnage/BasicClawsBladeStaffSecondFXTracker.prototype
        {  15932u, "Elektra" },  // Powers/Player/Elektra/KnifeThrowEffectNonBoss.prototype
        {  15933u, "Beast" },  // Powers/Player/Beast/MeleePBAoE.prototype
        {  15935u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/BFGHotspotSlow.prototype
        {  15937u, "Dr. Doom" },  // Powers/Player/DrDoom/DoomsDayAirStrikeSummon.prototype
        {  15939u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotBlockadeCallInTaunt.prototype
        {  15940u, "Cable" },  // Powers/Player/Cable/Talents/TechnoOrganicSoldierBuff.prototype
        {  15945u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/SignatureTKHurlPhoenixHotspotEffect.prototype
        {  15948u, "Venom" },  // Powers/Player/Venom/RangedPassiveHiddenPassive.prototype
        {  15950u, "Thor" },  // Powers/Player/Thor/Rework/OdinforceGainCombo120.prototype
        {  15952u, "Ant-Man" },  // Powers/Player/AntMan/AntWallKnockback.prototype
        {  15954u, "Hawkeye" },  // Powers/Player/Hawkeye/BasicArrow.prototype
        {  15955u, "Rogue" },  // Powers/Player/Rogue/RecallOverloadComboSummon.prototype
        {  15961u, "Beast" },  // Powers/Player/Beast/RagingBeastBuff.prototype
        {  15962u, "Thor" },  // Powers/Player/Thor/ChargeHasteCombo.prototype
        {  15964u, "Human Torch" },  // Powers/Player/HumanTorch/NovaBurstStackingEffect.prototype
        {  15965u, "Iceman" },  // Powers/Player/Iceman/Icewall.prototype
        {  15967u, "Iron Fist" },  // Powers/Player/IronFist/IronFistPunchStartMove.prototype
        {  15969u, "Wolverine" },  // Powers/Player/Wolverine/BasicRoninBleedVuln.prototype
        {  15970u, "Green Goblin" },  // Powers/Player/GreenGoblin/BombingCircleBomb.prototype
        {  15972u, "Human Torch" },  // Powers/Player/HumanTorch/NovaBurstEffect.prototype
        {  15974u, "Colossus" },  // Powers/Player/Colossus/MetalRegenerationCombo.prototype
        {  15975u, "Rogue" },  // Powers/Player/Rogue/StolenPowerLibrarySlot7.prototype
        {  15976u, "Beast" },  // Powers/Player/Beast/GlueBombDamageProc.prototype
        {  15977u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/Talents/SquirrelsFromAboveProcCombo.prototype
        {  15983u, "Blade" },  // Powers/Player/Blade/SerumOnCooldownBuff.prototype
        {  15985u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/IcemanIceGolem.prototype
        {  15990u, "Beast" },  // Powers/Player/Beast/ShieldGadgetSummonArea.prototype
        {  15992u, "Captain America" },  // Powers/Player/CaptainAmerica/ShieldThrowBleedEffect.prototype
        {  15993u, "Loki" },  // Powers/Player/Loki/GlacialSpike.prototype
        {  15995u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissilePowerSpeed500.prototype
        {  15997u, "Doctor Strange" },  // Powers/Player/DoctorStrange/VishantiSealMindlessOnePunchTwo.prototype
        {  15998u, "Captain America" },  // Powers/Player/CaptainAmerica/ShieldThrowPassiveBuffStack.prototype
        {  16002u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoMissileEffects/DoomMagicLanceMissileEffect.prototype
        {  16006u, "Cable" },  // Powers/Player/Cable/PsimitarLungeCooldownReduction.prototype
        {  16009u, "Angela" },  // Powers/Player/Angela/SpartaKick.prototype
        {  16012u, "Moon Knight" },  // Powers/Player/MoonKnight/SummonKhonshuStatueHotspotEffect.prototype
        {  16013u, "Beast" },  // Powers/Player/Beast/SleepGasGadget.prototype
        {  16014u, "Dr. Doom" },  // Powers/Player/DrDoom/FingerLasersHotspotEffect.prototype
        {  16016u, "Nova" },  // Powers/Player/Nova/PulsarHotspotProc.prototype
        {  16018u, "Black Widow" },  // Powers/Player/BlackWidow/Talents/FlipKickExplosives.prototype
        {  16020u, "Ultron" },  // Powers/Player/Ultron/EnergyDamageDefenseCondition.prototype
        {  16021u, "Iron Fist" },  // Powers/Player/IronFist/TigerClawStance.prototype
        {  16025u, "Winter Soldier" },  // Powers/Player/WinterSoldier/MeleeBuffProc.prototype
        {  16027u, "Dr. Doom" },  // Powers/Player/DrDoom/AoEDebuffMagicGainComboEffect.prototype
        {  16029u, "Gambit" },  // Powers/Player/Gambit/GandSlamEnhancedCDR.prototype
        {  16031u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/OutOfCombatStealth.prototype
        {  16032u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MagikMiniDemonLeapAttackEnd.prototype
        {  16034u, "Captain America" },  // Powers/Player/CaptainAmerica/Talents/ShieldBlockDurationIncSpec.prototype
        {  16036u, "Gambit" },  // Powers/Player/Gambit/JacksOrBetterStackingBuff.prototype
        {  16038u, "Elektra" },  // Powers/Player/Elektra/ShadowStrike.prototype
        {  16040u, "Psylocke" },  // Powers/Player/Psylocke/Talents/Talent3PsionicBarrierBuff.prototype
        {  16043u, "Taskmaster" },  // Powers/Player/Taskmaster/BrutalStrikeEffect.prototype
        {  16044u, "Ant-Man" },  // Powers/Player/AntMan/ShrinkConditionRemovalPower.prototype
        {  16048u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/PancakeRingDamageCombo.prototype
        {  16049u, "Vision" },  // Powers/Player/Vision/ControlRobotFullHealEffect.prototype
        {  16050u, "Angela" },  // Powers/Player/Angela/RibbonChannel.prototype
        {  16051u, "Wolverine" },  // Powers/Player/Wolverine/BerserkerBarrageEnhanced.prototype
        {  16053u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent4CallInBuffsWolverine.prototype
        {  16055u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Traits/ChaosOverloadRemove.prototype
        {  16058u, "Ant-Man" },  // Powers/Player/AntMan/Talents/AntUseSpiritTalent.prototype
        {  16059u, "Black Panther" },  // Powers/Player/BlackPanther/MineFieldActivationMelee.prototype
        {  16061u, "Colossus" },  // Powers/Player/Colossus/KittyPrydeSummon/PhaseAoE.prototype
        {  16065u, "Punisher" },  // Powers/Player/Punisher/Rework/ThreeRoundBurstMissileEffect.prototype
        {  16069u, "Loki" },  // Powers/Player/Loki/ConeOfMagicRemoveEnchantment.prototype
        {  16070u, "Emma Frost" },  // Powers/Player/EmmaFrost/UltimateTRexRoarHotspotEffect.prototype
        {  16071u, "Thor" },  // Powers/Player/Thor/Rework/SignatureAntiforceReflectCombo.prototype
        {  16073u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Traits/DefenseTrait.prototype
        {  16074u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/FlameWave.prototype
        {  16076u, "Captain America" },  // Powers/Player/CaptainAmerica/VibraniumStacks.prototype
        {  16077u, "Daredevil" },  // Powers/Player/Daredevil/UltimateKickCombo3.prototype
        {  16078u, "Elektra" },  // Powers/Player/Elektra/Talents/KillCommandStealthTalent.prototype
        {  16085u, "Iceman" },  // Powers/Player/Iceman/HotspotBeamHealingEffect.prototype
        {  16086u, "Deadpool" },  // Powers/Player/Deadpool/Rework/LilDeadpoolDollDeathEffectLarger.prototype
        {  16087u, "Magik" },  // Powers/Player/Magik/OtherworldlyNovaHealEnduranceGainCombo.prototype
        {  16090u, "Iron Man" },  // Powers/Player/IronMan/ForceShieldDamageAbsorptionShield.prototype
        {  16094u, "Black Cat" },  // Powers/Player/BlackCat/SignatureHotspotEffect.prototype
        {  16095u, "Vision" },  // Powers/Player/Vision/Talents/Talent1SolarRateRegenMax.prototype
        {  16096u, "Cyclops" },  // Powers/Player/Cyclops/Talents/ChanneledBeamUpgradeTalent.prototype
        {  16100u, "Winter Soldier" },  // Powers/Player/WinterSoldier/ArmBlastRemoveHighlight.prototype
        {  16102u, "Black Panther" },  // Powers/Player/BlackPanther/DoraSummonProc.prototype
        {  16107u, "Carnage" },  // Powers/Player/Carnage/AxeThrowMissileEffectAoE.prototype
        {  16113u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/FlyingKickEnd.prototype
        {  16114u, "Beast" },  // Powers/Player/Beast/MeleePBAoEDmgEffect.prototype
        {  16120u, "Luke Cage" },  // Powers/Player/LukeCage/ChargeProcEffect.prototype
        {  16121u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhasingPunch.prototype
        {  16123u, "Iceman" },  // Powers/Player/Iceman/UltimateSummonImpactPBAoE.prototype
        {  16126u, "Magneto" },  // Powers/Player/Magneto/SignatureMaelstrom.prototype
        {  16133u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SecondaryResourceResetThing.prototype
        {  16141u, "Cyclops" },  // Powers/Player/Cyclops/Rework/BasicPiercing.prototype
        {  16145u, "Thing" },  // Powers/Player/Thing/CallOutEnemiesComboEffect.prototype
        {  16146u, "Nova" },  // Powers/Player/Nova/ChargedDash.prototype
        {  16149u, "Black Bolt" },  // Powers/Player/BlackBolt/HypersonicScreamHotspotSummonCombo.prototype
        {  16151u, "Storm" },  // Powers/Player/Storm/ZephyrKnockback.prototype
        {  16155u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BoardSweep.prototype
        {  16156u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HulkSmashBrutalChance.prototype
        {  16158u, "Black Cat" },  // Powers/Player/BlackCat/BasicClawsHealthOnHitCombo.prototype
        {  16159u, "Cable" },  // Powers/Player/Cable/PsimitarLungeHiddenPassive.prototype
        {  16165u, "War Machine" },  // Powers/Player/WarMachine/Talents/Talent2MissileTargeting.prototype
        {  16169u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/DoctorStrangeFangNukeHiddenPassi.prototype
        {  16173u, "Nova" },  // Powers/Player/Nova/PulsarHotspotSummonProcEffectRR.prototype
        {  16176u, "Loki" },  // Powers/Player/Loki/IllusionTooltipDriver.prototype
        {  16177u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/HammerFistBonus.prototype
        {  16178u, "Beast" },  // Powers/Player/Beast/HulkingSlam.prototype
        {  16180u, "Nick Fury" },  // Powers/Player/NickFury/RocketLauncherRingDamageCombo.prototype
        {  16184u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/StretchyDashMissileCombo.prototype
        {  16188u, "Loki" },  // Powers/Player/Loki/LightBeamSummonCombo.prototype
        {  16190u, "Black Cat" },  // Powers/Player/BlackCat/UltimateHotspotEffect.prototype
        {  16191u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GravityMineVulnerabilityCombo.prototype
        {  16192u, "Cyclops" },  // Powers/Player/Cyclops/Rework/CallJean.prototype
        {  16194u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/BlackHoleExplosion.prototype
        {  16195u, "Nova" },  // Powers/Player/Nova/FuriousLunge.prototype
        {  16198u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/RhinoBigChargeProcEffect.prototype
        {  16200u, "X-23" },  // Powers/Player/X23/Traits/MechanicTrait.prototype
        {  16202u, "Black Cat" },  // Powers/Player/BlackCat/Talents/TalentMasterThiefStealChance.prototype
        {  16204u, "Loki" },  // Powers/Player/Loki/IllusionRushSummonComboMore.prototype
        {  16206u, "Psylocke" },  // Powers/Player/Psylocke/BowMissileEffect.prototype
        {  16207u, "Taskmaster" },  // Powers/Player/Taskmaster/TripleStrike.prototype
        {  16211u, "She-Hulk" },  // Powers/Player/SheHulk/ObjectionCritChanceCombo.prototype
        {  16213u, "Iceman" },  // Powers/Player/Iceman/ChanneledBeamHotspotEffectVisual.prototype
        {  16214u, "Deadpool" },  // Powers/Player/Deadpool/SerratedBladeComboDoT.prototype
        {  16215u, "Captain America" },  // Powers/Player/CaptainAmerica/FinestHourEnd.prototype
        {  16218u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/BasicBeamsBuffs.prototype
        {  16219u, "Cable" },  // Powers/Player/Cable/TelepathicIllusionOnDeathProc.prototype
        {  16222u, "Daredevil" },  // Powers/Player/Daredevil/Update/NunchuckAttack.prototype
        {  16225u, "Cable" },  // Powers/Player/Cable/Talents/ImpaleLayer.prototype
        {  16226u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TKTossPhoenixEffect.prototype
        {  16233u, "Blade" },  // Powers/Player/Blade/HemoglycerinGrenadeExplosionOnDeath.prototype
        {  16235u, "Cyclops" },  // Powers/Player/Cyclops/Talents/FocusBeamThirdStack.prototype
        {  16239u, "Angela" },  // Powers/Player/Angela/DoubleAxeThrowExecuteCombo.prototype
        {  16240u, "Magik" },  // Powers/Player/Magik/DarkPactConsumeCommand.prototype
        {  16241u, "X-23" },  // Powers/Player/X23/CrimsonLeapEnd.prototype
        {  16242u, "Storm" },  // Powers/Player/Storm/Talents/StormSurgeInstantFill.prototype
        {  16243u, "Thor" },  // Powers/Player/Thor/Rework/UltimateGodBlastBrutal.prototype
        {  16244u, "Juggernaut" },  // Powers/Player/Juggernaut/RemoveHighlightCombo.prototype
        {  16245u, "Hulk" },  // Powers/Player/Hulk/Talents/Talent3TantrumBonus.prototype
        {  16246u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DamageMaelstromPhoenix.prototype
        {  16247u, "Ghost Rider" },  // Powers/Player/GhostRider/PassiveRegenRevivalHealOverTime.prototype
        {  16248u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/SquirrelDamageMult.prototype
        {  16249u, "Angela" },  // Powers/Player/Angela/MiraculousAssaultHotspotEffect.prototype
        {  16251u, "Angela" },  // Powers/Player/Angela/HackSlashHealthGain.prototype
        {  16256u, "Cyclops" },  // Powers/Player/Cyclops/RoundhouseMeleeBuff.prototype
        {  16258u, "Black Widow" },  // Powers/Player/BlackWidow/FlipKickNoMovement.prototype
        {  16260u, "Loki" },  // Powers/Player/Loki/FrostNovaChillFilterPower.prototype
        {  16261u, "Magik" },  // Powers/Player/Magik/Talents/SoulConeSpender.prototype
        {  16262u, "Human Torch" },  // Powers/Player/HumanTorch/UltimateHiddenPassive.prototype
        {  16263u, "War Machine" },  // Powers/Player/WarMachine/ThermalShot.prototype
        {  16265u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/WarpTurretShieldGenerationHotspotEffect.prototype
        {  16266u, "Nick Fury" },  // Powers/Player/NickFury/Talents/Talent3FireteamMedic.prototype
        {  16267u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/PummelStartCombo.prototype
        {  16268u, "Psylocke" },  // Powers/Player/Psylocke/BowDecoyPower.prototype
        {  16269u, "Black Panther" },  // Powers/Player/BlackPanther/DoraMilajeDefaultAttack.prototype
        {  16270u, "Storm" },  // Powers/Player/Storm/ChargedStrike.prototype
        {  16271u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/UltimateDeathFromAbove.prototype
        {  16274u, "Iron Fist" },  // Powers/Player/IronFist/Talents/Talent5FiveStance.prototype
        {  16275u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/WarpTurret.prototype
        {  16276u, "Vision" },  // Powers/Player/Vision/Talents/Talent5SigCDR.prototype
        {  16279u, "Vision" },  // Powers/Player/Vision/EnhanceRobotBuffControlledRobotBuff.prototype
        {  16280u, "Venom" },  // Powers/Player/Venom/WrithingTendrils.prototype
        {  16281u, "Psylocke" },  // Powers/Player/Psylocke/SeekerButterflies.prototype
        {  16282u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LokiIllusionRushDecoyPower.prototype
        {  16283u, "Nick Fury" },  // Powers/Player/NickFury/Talents/Talent1CommandingShoutRemap.prototype
        {  16285u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/ForceWallBonus.prototype
        {  16286u, "Nova" },  // Powers/Player/Nova/PulsarExplosion.prototype
        {  16290u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Traits/MechanicTraitAmmoShields.prototype
        {  16294u, "Doctor Strange" },  // Powers/Player/DoctorStrange/IcyTendrils.prototype
        {  16298u, "Punisher" },  // Powers/Player/Punisher/Rework/RpgKeywordConditionCombo.prototype
        {  16300u, "Elektra" },  // Powers/Player/Elektra/MarkForDeathDiveBombCombo.prototype
        {  16305u, "Iron Fist" },  // Powers/Player/IronFist/ChiPunchAsCombo.prototype
        {  16309u, "Cable" },  // Powers/Player/Cable/ViperBeamPlusHotspotEffect.prototype
        {  16311u, "Iron Fist" },  // Powers/Player/IronFist/FlyingKickSummonCombo.prototype
        {  16312u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Haymaker.prototype
        {  16313u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AxeHeelDrop.prototype
        {  16315u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AmpControlledMobTargetIllusion.prototype
        {  16317u, "Elektra" },  // Powers/Player/Elektra/BlowDartSummonHotspotEffect.prototype
        {  16320u, "Magneto" },  // Powers/Player/Magneto/Talents/MetalObjectSmashBonus.prototype
        {  16321u, "Nightcrawler" },  // Powers/Player/Nightcrawler/ValiantLeapHideMesh.prototype
        {  16323u, "Ghost Rider" },  // Powers/Player/GhostRider/Talents/Talent2MentalBuild.prototype
        {  16324u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ToadTongueYankDoT.prototype
        {  16325u, "Hawkeye" },  // Powers/Player/Hawkeye/TrickQuiverComboPoisonGasBombProc.prototype
        {  16327u, "War Machine" },  // Powers/Player/WarMachine/HeatGainChainGunBurst.prototype
        {  16328u, "Colossus" },  // Powers/Player/Colossus/Talents/Talent5SigCooldownResets.prototype
        {  16336u, "Magik" },  // Powers/Player/Magik/LifeTapConfuse.prototype
        {  16340u, "Magik" },  // Powers/Player/Magik/SorcerousEruption.prototype
        {  16341u, "Iron Man" },  // Powers/Player/IronMan/ChanneledEnergyBeam.prototype
        {  16347u, "Loki" },  // Powers/Player/Loki/Ultimate.prototype
        {  16348u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/AstralWhip.prototype
        {  16355u, "Cable" },  // Powers/Player/Cable/KineticRepulsionLargeKnockback.prototype
        {  16356u, "Daredevil" },  // Powers/Player/Daredevil/BouncingStrikeChainPower.prototype
        {  16357u, "Venom" },  // Powers/Player/Venom/Talents/WrithingTendrilsBuff.prototype
        {  16358u, "Loki" },  // Powers/Player/Loki/MeddlingStrikeIllusionSummonComboMore.prototype
        {  16359u, "Cable" },  // Powers/Player/Cable/TKOverload.prototype
        {  16360u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/InvisibilityStackingBuff.prototype
        {  16361u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/SpikedBallSkillshot.prototype
        {  16362u, "Hawkeye" },  // Powers/Player/Hawkeye/Tumble.prototype
        {  16367u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GrootOutProcMissileEffect.prototype
        {  16368u, "Beast" },  // Powers/Player/Beast/SleepGasMarkerGasAreaSummon.prototype
        {  16371u, "Deadpool" },  // Powers/Player/Deadpool/Rework/AwesomeHiddenPassive.prototype
        {  16373u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/ChargeBeamMissileEffect.prototype
        {  16376u, "Winter Soldier" },  // Powers/Player/WinterSoldier/Talents/Talent3BionicEMP.prototype
        {  16377u, "Ant-Man" },  // Powers/Player/AntMan/DisruptorBlastDamageMultForShrink.prototype
        {  16378u, "Blade" },  // Powers/Player/Blade/BloodlustRisesBleeding.prototype
        {  16379u, "Nightcrawler" },  // Powers/Player/Nightcrawler/Traits/DefenseTrait.prototype
        {  16380u, "Black Bolt" },  // Powers/Player/BlackBolt/Traits/MechanicTraitEnergyManipulator.prototype
        {  16382u, "Hulk" },  // Powers/Player/Hulk/Rework/Tantrum.prototype
        {  16385u, "Green Goblin" },  // Powers/Player/GreenGoblin/Talents/HallucinogenicPumpkinTalent.prototype
        {  16389u, "Black Panther" },  // Powers/Player/BlackPanther/Talents/DoraDefensiveTalent.prototype
        {  16390u, "Moon Knight" },  // Powers/Player/MoonKnight/CrescentDartFanMissileEffect.prototype
        {  16392u, "Nick Fury" },  // Powers/Player/NickFury/Traits/DefaultAmmoBelow25Pct.prototype
        {  16394u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/DarkPhoenixPhoenixMaelstromSummon.prototype
        {  16395u, "Carnage" },  // Powers/Player/Carnage/ClawPummelDamageCombo.prototype
        {  16396u, "Psylocke" },  // Powers/Player/Psylocke/PsiBarrierMechanics.prototype
        {  16402u, "Colossus" },  // Powers/Player/Colossus/SiberianExpressHotspotDoT.prototype
        {  16405u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/SilverSurferChanneledBeamSelfAudio.prototype
        {  16406u, "Dr. Doom" },  // Powers/Player/DrDoom/Missiles.prototype
        {  16409u, "Black Bolt" },  // Powers/Player/BlackBolt/Talents/Talent1EnergyPassive.prototype
        {  16411u, "Carnage" },  // Powers/Player/Carnage/YankImpale.prototype
        {  16412u, "Wolverine" },  // Powers/Player/Wolverine/BasicBleed.prototype
        {  16413u, "Magik" },  // Powers/Player/Magik/SpitterDemonSummonCombo.prototype
        {  16417u, "Taskmaster" },  // Powers/Player/Taskmaster/WebSplatHotspotEffect.prototype
        {  16419u, "War Machine" },  // Powers/Player/WarMachine/WarMachineArmorCCImmuneCombo.prototype
        {  16421u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseOut.prototype
        {  16423u, "Black Bolt" },  // Powers/Player/BlackBolt/GeyserVulnerabilityHSEffect.prototype
        {  16425u, "Carnage" },  // Powers/Player/Carnage/Talents/BloodlustDecay.prototype
        {  16428u, "Ant-Man" },  // Powers/Player/AntMan/ShrinkingStrikeSpiritGainComboEffect.prototype
        {  16430u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/QuicksilverSupoersonicCycloneImplosion.prototype
        {  16431u, "Human Torch" },  // Powers/Player/HumanTorch/Traits/DefenseTrait.prototype
        {  16432u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/TKTossPhoenix.prototype
        {  16433u, "Nightcrawler" },  // Powers/Player/Nightcrawler/ShadowTeamBuffEffect.prototype
        {  16437u, "Rogue" },  // Powers/Player/Rogue/UltimateHiddenPassiveRanks.prototype
        {  16439u, "Iron Man" },  // Powers/Player/IronMan/MissileCritPassiveProc.prototype
        {  16443u, "Hawkeye" },  // Powers/Player/Hawkeye/Talents/SerratedArrowheadsTalent.prototype
        {  16444u, "Ghost Rider" },  // Powers/Player/GhostRider/UltimatePillarHSEffect.prototype
        {  16446u, "Doctor Strange" },  // Powers/Player/DoctorStrange/BasicBolts.prototype
        {  16452u, "Punisher" },  // Powers/Player/Punisher/Rework/SMGMissileEffect.prototype
        {  16454u, "Vision" },  // Powers/Player/Vision/DFASolarBlastCombo.prototype
        {  16457u, "Black Cat" },  // Powers/Player/BlackCat/ClawPummel4.prototype
        {  16458u, "Daredevil" },  // Powers/Player/Daredevil/ComboPointGainMechanic.prototype
        {  16459u, "Punisher" },  // Powers/Player/Punisher/Rework/UltimateBattleVanAutoGunEffect.prototype
        {  16462u, "Cable" },  // Powers/Player/Cable/FutureBombEnergyExplosionPsimitarKeyword.prototype
        {  16463u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseAOEWeakenComboLarge.prototype
        {  16471u, "Thor" },  // Powers/Player/Thor/Rework/ShockwaveHotspotSummon.prototype
        {  16472u, "Dr. Doom" },  // Powers/Player/DrDoom/DoomBotBasicPunch.prototype
        {  16473u, "Nova" },  // Powers/Player/Nova/PulsarExplosionIncrementCharge.prototype
        {  16476u, "Carnage" },  // Powers/Player/Carnage/MacePummel.prototype
        {  16477u, "Ghost Rider" },  // Powers/Player/GhostRider/Talents/Talent2LowHealthBuild.prototype
        {  16479u, "Storm" },  // Powers/Player/Storm/Zephyr.prototype
        {  16480u, "Kitty Pryde" },  // Powers/Player/KittyPryde/BuddySystem.prototype
        {  16482u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/OrbStormHotspotEffect.prototype
        {  16484u, "Colossus" },  // Powers/Player/Colossus/WolverineSummon/DefaultAttack2.prototype
        {  16486u, "Magik" },  // Powers/Player/Magik/SoulCaptureMinionBuffHotspotEffect.prototype
        {  16489u, "War Machine" },  // Powers/Player/WarMachine/ChaingunBurst.prototype
        {  16495u, "Blade" },  // Powers/Player/Blade/AdvancedTechniqueCombo2Spin.prototype
        {  16497u, "Cable" },  // Powers/Player/Cable/ParticleAccelerator.prototype
        {  16501u, "Ultron" },  // Powers/Player/Ultron/DisengagingShot.prototype
        {  16502u, "Daredevil" },  // Powers/Player/Daredevil/Update/ClubRicochetBleedMissileEffect.prototype
        {  16505u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/SlowAoE.prototype
        {  16506u, "Storm" },  // Powers/Player/Storm/Microburst.prototype
        {  16508u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotThumperCallInGroundPoundSlowEffect.prototype
        {  16509u, "Doctor Strange" },  // Powers/Player/DoctorStrange/EssenceOfZom.prototype
        {  16514u, "Iron Man" },  // Powers/Player/IronMan/BoltSpray.prototype
        {  16516u, "Cyclops" },  // Powers/Player/Cyclops/BasicPunch.prototype
        {  16517u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/UltimateFFStartImmobilize.prototype
        {  16519u, "Deadpool" },  // Powers/Player/Deadpool/Talents/GunsGloriousGunsTalent.prototype
        {  16521u, "Cyclops" },  // Powers/Player/Cyclops/Talents/SigCooldownTimeTalent.prototype
        {  16522u, "Blade" },  // Powers/Player/Blade/DeathFromAboveEnd.prototype
        {  16523u, "Elektra" },  // Powers/Player/Elektra/Talents/ShadowStrikeDiveBombAutoMark.prototype
        {  16528u, "Human Torch" },  // Powers/Player/HumanTorch/Traits/OverheatEffectTooHotToHit.prototype
        {  16534u, "Magneto" },  // Powers/Player/Magneto/Talents/RapidFire.prototype
        {  16535u, "Punisher" },  // Powers/Player/Punisher/Traits/DefaultAmmoBelow25Pct.prototype
        {  16542u, "Punisher" },  // Powers/Player/Punisher/Traits/OffenseTrait.prototype
        {  16543u, "Taskmaster" },  // Powers/Player/Taskmaster/WebSplat.prototype
        {  16544u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/CrusherHotspot.prototype
        {  16546u, "Green Goblin" },  // Powers/Player/GreenGoblin/SonicToadDeathEffect.prototype
        {  16548u, "Ghost Rider" },  // Powers/Player/GhostRider/ChainsAblaze.prototype
        {  16551u, "Psylocke" },  // Powers/Player/Psylocke/DashBackstabComboDecoyPower.prototype
        {  16552u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/NovaBlastoffEffect.prototype
        {  16553u, "Nova" },  // Powers/Player/Nova/ChargedDashEffect.prototype
        {  16554u, "Ant-Man" },  // Powers/Player/AntMan/BounceDashConditionPower.prototype
        {  16555u, "Cable" },  // Powers/Player/Cable/ViperBeamHotspotEffect.prototype
        {  16556u, "Dr. Doom" },  // Powers/Player/DrDoom/DoomsDayMissiles.prototype
        {  16558u, "Psylocke" },  // Powers/Player/Psylocke/BowComboBuff.prototype
        {  16559u, "Cable" },  // Powers/Player/Cable/KineticBarrier.prototype
        {  16562u, "Rogue" },  // Powers/Player/Rogue/UltimateRaginCajun.prototype
        {  16567u, "Green Goblin" },  // Powers/Player/GreenGoblin/Talents/MoreToadsTalent.prototype
        {  16569u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/FirestarEnergyRainEffect.prototype
        {  16572u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/Talents/BouncyBuildResetCharges.prototype
        {  16573u, "Thor" },  // Powers/Player/Thor/Rework/ImmortalCombatHiddenPassive.prototype
        {  16574u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveLizardProcEffect.prototype
        {  16575u, "Taskmaster" },  // Powers/Player/Taskmaster/SwappingPassiveDefenseDodgeProc.prototype
        {  16580u, "Emma Frost" },  // Powers/Player/EmmaFrost/DiamondHeartSecondaryResourceFill.prototype
        {  16581u, "Loki" },  // Powers/Player/Loki/SoulCrush.prototype
        {  16584u, "Iceman" },  // Powers/Player/Iceman/FocusBeamShatterDamageMult.prototype
        {  16585u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/LiftAndSlamCDR.prototype
        {  16586u, "Carnage" },  // Powers/Player/Carnage/BasicClawsMaceWasUsedLast.prototype
        {  16590u, "Nova" },  // Powers/Player/Nova/HeavyBlastBuffFromPulsar.prototype
        {  16591u, "Jean Grey" },  // Powers/Player/JeanGrey/Talents/KineticWaveBonus.prototype
        {  16592u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosHexHotspotEffect.prototype
        {  16595u, "Magik" },  // Powers/Player/Magik/LifeTapHiddenPassive.prototype
        {  16596u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/StretchyDashEnd.prototype
        {  16598u, "War Machine" },  // Powers/Player/WarMachine/Talents/Talent4Overclocking.prototype
        {  16603u, "Venom" },  // Powers/Player/Venom/RangedBasicMissileEffect.prototype
        {  16604u, "Dr. Doom" },  // Powers/Player/DrDoom/Talents/Talent2CrumbleFools.prototype
        {  16605u, "Human Torch" },  // Powers/Player/HumanTorch/HomingShot.prototype
        {  16606u, "Gambit" },  // Powers/Player/Gambit/BasicKineticCardHiddenPassive.prototype
        {  16608u, "Angela" },  // Powers/Player/Angela/DisablingRibbonsAsProc.prototype
        {  16610u, "Beast" },  // Powers/Player/Beast/BeastDashAreaEffect.prototype
        {  16615u, "Angela" },  // Powers/Player/Angela/SpiritGainProc.prototype
        {  16616u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedAutoAttackAoE.prototype
        {  16618u, "Iron Fist" },  // Powers/Player/IronFist/Traits/DefenseTrait.prototype
        {  16622u, "Beast" },  // Powers/Player/Beast/MeleePBAoEVulnEffect.prototype
        {  16626u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PhaseToggleOutOfCombatDelayed.prototype
        {  16628u, "Luke Cage" },  // Powers/Player/LukeCage/GoodAtCombosCritDamageBuff.prototype
        {  16634u, "War Machine" },  // Powers/Player/WarMachine/TeslaFieldEffect.prototype
        {  16635u, "Cable" },  // Powers/Player/Cable/KineticBarrierHotspotEffect.prototype
        {  16637u, "Cable" },  // Powers/Player/Cable/Ultimate.prototype
        {  16643u, "Ultron" },  // Powers/Player/Ultron/Talents/Talent3SelfRez.prototype
        {  16645u, "Black Cat" },  // Powers/Player/BlackCat/Talents/TalentTumbleStealthDuration.prototype
        {  16646u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveThingThornsProc.prototype
        {  16647u, "Iceman" },  // Powers/Player/Iceman/IceGolem.prototype
        {  16648u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/GrootDeathFromAboveComboMovieVol2.prototype
        {  16649u, "Doctor Strange" },  // Powers/Player/DoctorStrange/Talents/ShieldOfSeraphimAutoshield.prototype
        {  16657u, "Doctor Strange" },  // Powers/Player/DoctorStrange/PassiveDamageProcEffect.prototype
        {  16658u, "Hawkeye" },  // Powers/Player/Hawkeye/CriticalChancePassiveBuffProcEff.prototype
        {  16660u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/KaeciliusHealChannel.prototype
        {  16664u, "Rogue" },  // Powers/Player/Rogue/GlovesOff.prototype
        {  16671u, "Taskmaster" },  // Powers/Player/Taskmaster/Talents/IronFistTechniqueSnakeStanceBuffDoT.prototype
        {  16672u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ChargedPBAoE.prototype
        {  16677u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MinigunMissileEffect.prototype
        {  16678u, "Ultron" },  // Powers/Player/Ultron/Talents/Talent4CommandingShotBuff.prototype
        {  16682u, "Vision" },  // Powers/Player/Vision/Talents/Talent4NoPetBuff.prototype
        {  16687u, "Angela" },  // Powers/Player/Angela/SignatureEndAndReset.prototype
        {  16688u, "Ant-Man" },  // Powers/Player/AntMan/AntAllyBuffsHiddenPassive.prototype
        {  16689u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveStarlordBuffProcEffect.prototype
        {  16690u, "Nova" },  // Powers/Player/Nova/SignatureSupernova.prototype
        {  16691u, "Punisher" },  // Powers/Player/Punisher/Rework/Rpg.prototype
        {  16692u, "X-23" },  // Powers/Player/X23/CrimsonLeapStart.prototype
        {  16693u, "Iceman" },  // Powers/Player/Iceman/AbsoluteZeroHotspotEffect.prototype
        {  16695u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/zzzDeprecated/IronFistChiBurst.prototype
        {  16696u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ArmyFromNothingIntervalEffectWiccan.prototype
        {  16701u, "Magneto" },  // Powers/Player/Magneto/ShrapnelAuraMissileEffect.prototype
        {  16702u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/HERBIEProtectedBuff.prototype
        {  16708u, "Magik" },  // Powers/Player/Magik/OtherworldlyNovaExpandingMissileCombo.prototype
        {  16710u, "Black Bolt" },  // Powers/Player/BlackBolt/ChanneledBeam.prototype
        {  16712u, "Storm" },  // Powers/Player/Storm/MobilityWindBuffHotspotEffect.prototype
        {  16716u, "Blade" },  // Powers/Player/Blade/UnleashGlaiveMissileEffectCooldown.prototype
        {  16717u, "Rogue" },  // Powers/Player/Rogue/UppercutBleedCombo.prototype
        {  16718u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ShieldedFist.prototype
        {  16719u, "Gambit" },  // Powers/Player/Gambit/Talents/Talent1RaginCajun.prototype
        {  16720u, "Doctor Strange" },  // Powers/Player/DoctorStrange/EssenceOfZomSummonHotspot.prototype
        {  16721u, "Loki" },  // Powers/Player/Loki/ConeOfMagicEnchanterBuff.prototype
        {  16723u, "Cyclops" },  // Powers/Player/Cyclops/TeamSteroid.prototype
        {  16724u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/ChaosRiftAsCombo.prototype
        {  16728u, "Vision" },  // Powers/Player/Vision/BigPunch.prototype
        {  16731u, "Venom" },  // Powers/Player/Venom/FearCleanseCCImmuneComboNoTendri.prototype
        {  16732u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoThrownObjectDamage.prototype
        {  16734u, "Venom" },  // Powers/Player/Venom/SymbioteSummonAttack2.prototype
        {  16742u, "Black Bolt" },  // Powers/Player/BlackBolt/Echo.prototype
        {  16743u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/HellfireDoTAuraHotspotEffect.prototype
        {  16744u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateCallIcemanSlowEffect.prototype
        {  16745u, "Nova" },  // Powers/Player/Nova/Talents/Talent3MicroUnstablePulsar.prototype
        {  16747u, "Colossus" },  // Powers/Player/Colossus/FastballSpecialThrownMissile.prototype
        {  16748u, "Angela" },  // Powers/Player/Angela/DFAFirstActivation.prototype
        {  16750u, "Nova" },  // Powers/Player/Nova/LungingPunchBuff.prototype
        {  16751u, "Ant-Man" },  // Powers/Player/AntMan/GiantManFoot.prototype
        {  16758u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ElektraShadowStrikeCDR.prototype
        {  16763u, "Magik" },  // Powers/Player/Magik/SoulShockwaveMissileEffectTooltip.prototype
        {  16765u, "Thing" },  // Powers/Player/Thing/Rework/AuraToughnessComboExclusive.prototype
        {  16768u, "Vision" },  // Powers/Player/Vision/DashMeleeDamageBonus.prototype
        {  16771u, "Psylocke" },  // Powers/Player/Psylocke/PassiveDefense.prototype
        {  16773u, "Jean Grey" },  // Powers/Player/JeanGrey/PhoenixModeToggleRezInvulnerableEffect.prototype
        {  16779u, "Emma Frost" },  // Powers/Player/EmmaFrost/Traits/DiamondFormCondition.prototype
        {  16781u, "Deadpool" },  // Powers/Player/Deadpool/Rework/GiantMalletBiggerAoE.prototype
        {  16782u, "She-Hulk" },  // Powers/Player/SheHulk/BarristerBeatdown7thHit.prototype
        {  16784u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ImplodeExplodeFocusGainCombo.prototype
        {  16785u, "Hulk" },  // Powers/Player/Hulk/Rework/BasicMeleeUtil.prototype
        {  16786u, "Deadpool" },  // Powers/Player/Deadpool/Rework/DeadpoolnadoHotspot.prototype
        {  16788u, "Thor" },  // Powers/Player/Thor/Rework/ElectricallyChargedExtraStrike.prototype
        {  16791u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/SlowAoEDoTComboJean.prototype
        {  16792u, "Jean Grey" },  // Powers/Player/JeanGrey/Rework/NeuralNetworkPhoenixAoEProc2ndWave.prototype
        {  16795u, "Dr. Doom" },  // Powers/Player/DrDoom/DoombotFlyerSkillshot.prototype
        {  16799u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/MindlessOneBeamEndExplosion.prototype
        {  16800u, "Nova" },  // Powers/Player/Nova/PassiveSpeedDamageReductionPctBuff.prototype
        {  16804u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/JessicaJones/JessicaJonesDefaultAttack2.prototype
        {  16807u, "Daredevil" },  // Powers/Player/Daredevil/Update/Vault.prototype
        {  16808u, "Thor" },  // Powers/Player/Thor/Rework/BoltSpray.prototype
        {  16815u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/DefaultAttack4.prototype
        {  16818u, "Magik" },  // Powers/Player/Magik/LifeTapDoTCorpseExplosion.prototype
        {  16821u, "Green Goblin" },  // Powers/Player/GreenGoblin/PBAoESpinDoT.prototype
        {  16822u, "Ultron" },  // Powers/Player/Ultron/SuicideDroneSelfRezProcEffect.prototype
        {  16823u, "Taskmaster" },  // Powers/Player/Taskmaster/StudentsBuffPopcorn.prototype
        {  16824u, "Deadpool" },  // Powers/Player/Deadpool/AcrobaticAttackSecondaryStun.prototype
        {  16825u, "X-23" },  // Powers/Player/X23/Talents/Talent2PummelExecuteDmgCDR.prototype
        {  16826u, "Nova" },  // Powers/Player/Nova/PulsarExplosionEffect.prototype
        {  16829u, "Luke Cage" },  // Powers/Player/LukeCage/SummonMistyKnight.prototype
        {  16830u, "Black Widow" },  // Powers/Player/BlackWidow/WidowsBiteVisualBeam.prototype
        {  16831u, "Silver Surfer" },  // Powers/Player/SilverSurfer/ForceFieldDamageAbsorptionShield.prototype
        {  16834u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/GiantGunGadgetAddCharge.prototype
        {  16835u, "Daredevil" },  // Powers/Player/Daredevil/Update/ComboPointHiddenPassive.prototype
        {  16840u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CosmicRift.prototype
        {  16846u, "Green Goblin" },  // Powers/Player/GreenGoblin/Talents/TheBigOneBuffTalent.prototype
        {  16847u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/ImplosionGadgetPulsingController.prototype
        {  16848u, "Dr. Doom" },  // Powers/Player/DrDoom/FingerLasersPvPCooldownActiveShort.prototype
        {  16849u, "Human Torch" },  // Powers/Player/HumanTorch/BasicFireWedgeHeatSpender.prototype
        {  16852u, "Magik" },  // Powers/Player/Magik/BloodSpiritVisualCharges.prototype
        {  16853u, "Black Cat" },  // Powers/Player/BlackCat/Talents/TalentMasterThiefResetTrap.prototype
        {  16855u, "Psylocke" },  // Powers/Player/Psylocke/KatanaLeapSlashAoEDoT.prototype
        {  16856u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CosmicGiftRemoval.prototype
        {  16857u, "Deadpool" },  // Powers/Player/Deadpool/Rework/LilDeadpoolDollDeathProc.prototype
        {  16858u, "Wolverine" },  // Powers/Player/Wolverine/RawrWeakenCombo.prototype
        {  16859u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/StepfordUltimateHotspotEffect.prototype
        {  16860u, "Hawkeye" },  // Powers/Player/Hawkeye/BasicMelee.prototype
        {  16862u, "Iceman" },  // Powers/Player/Iceman/FrozenOrb.prototype
        {  16865u, "Dr. Doom" },  // Powers/Player/DrDoom/UnworthyPistol.prototype
        {  16869u, "Gambit" },  // Powers/Player/Gambit/UltimateComboSummonsRogue.prototype
        {  16872u, "Beast" },  // Powers/Player/Beast/Talents/Talent4AutoShield.prototype
        {  16878u, "Jean Grey" },  // Powers/Player/JeanGrey/DebrisMaelstromHotspotEffectPhoenix.prototype
        {  16879u, "Hulk" },  // Powers/Player/Hulk/Rework/WorldbreakerBuffCombo.prototype
        {  16881u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GrootDeathEffect.prototype
        {  16886u, "Taskmaster" },  // Powers/Player/Taskmaster/UltimateHawkeyeTurretArrowHotspotEffect.prototype
        {  16887u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveTaskmaster.prototype
        {  16889u, "Taskmaster" },  // Powers/Player/Taskmaster/DisengagingShot.prototype
        {  16890u, "Silver Surfer" },  // Powers/Player/SilverSurfer/CleanseCCImmuneCombo.prototype
        {  16891u, "Iron Fist" },  // Powers/Player/IronFist/SnakeStanceEnduranceMaterialOverride.prototype
        {  16893u, "Nightcrawler" },  // Powers/Player/Nightcrawler/UltimateInvulnConditionPower.prototype
        {  16894u, "War Machine" },  // Powers/Player/WarMachine/HeatDecayPreventer.prototype
        {  16896u, "Ultron" },  // Powers/Player/Ultron/RangeDroneRepulsorBeam.prototype
        {  16898u, "Iron Man" },  // Powers/Player/IronMan/MissileSalvoEffect.prototype
        {  16901u, "Beast" },  // Powers/Player/Beast/BeastBamfBrosAoEHit.prototype
        {  16902u, "Human Torch" },  // Powers/Player/HumanTorch/Ultimate.prototype
        {  16905u, "Psylocke" },  // Powers/Player/Psylocke/DashStealthEffect.prototype
        {  16907u, "Hulk" },  // Powers/Player/Hulk/Rework/MeteorImplosionCombo.prototype
        {  16909u, "Kitty Pryde" },  // Powers/Player/KittyPryde/PullUnderNormals.prototype
        {  16910u, "Silver Surfer" },  // Powers/Player/SilverSurfer/Talents/CosmicGift.prototype
        {  16911u, "X-23" },  // Powers/Player/X23/Pummel3.prototype
        {  16912u, "Venom" },  // Powers/Player/Venom/Talents/TentacleImpaleBuff.prototype
        {  16913u, "Vision" },  // Powers/Player/Vision/DashProcEffect.prototype
        {  16916u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/BasicRangedSquirrelPiercing.prototype
        {  16922u, "Ghost Rider" },  // Powers/Player/GhostRider/FirePillarHotspotEffect.prototype
        {  16923u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/GrootRideGrootHotspotSummon.prototype
        {  16925u, "Jean Grey" },  // Powers/Player/JeanGrey/PhoenixForceGainMechanic.prototype
        {  16927u, "Cyclops" },  // Powers/Player/Cyclops/FocusBeamNew.prototype
        {  16928u, "Loki" },  // Powers/Player/Loki/IceShardMissileEffect.prototype
        {  16930u, "Thing" },  // Powers/Player/Thing/ThickSkinDoT.prototype
        {  16931u, "Mr. Fantastic" },  // Powers/Player/MrFantastic/HERBIERapidHealing.prototype
        {  16934u, "Ghost Rider" },  // Powers/Player/GhostRider/HellfireBeam.prototype
        {  16938u, "Rocket Raccoon" },  // Powers/Player/RocketRaccoon/Rework/PlasmaCannonHotspotSlow.prototype
        {  16942u, "Hawkeye" },  // Powers/Player/Hawkeye/Traits/TrickQuiverMechanicTrait.prototype
        {  16945u, "Black Bolt" },  // Powers/Player/BlackBolt/EchoMissileEffect.prototype
        {  16946u, "Deadpool" },  // Powers/Player/Deadpool/Rework/MultiplayerPirateDeadpoolGrenado.prototype
        {  16948u, "Angela" },  // Powers/Player/Angela/NonExecuteCombo.prototype
        {  16949u, "Iron Fist" },  // Powers/Player/IronFist/SevenSidedStrike.prototype
        {  16950u, "Ultron" },  // Powers/Player/Ultron/MeleeStrike.prototype
        {  16954u, "Emma Frost" },  // Powers/Player/EmmaFrost/Talents/KneelBeforeMeCDR.prototype
        {  16955u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveHoodBuffProcEffect.prototype
        {  16957u, "Winter Soldier" },  // Powers/Player/WinterSoldier/StealthRollSniperShotCritBuff.prototype
        {  16959u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/IronMaidenEnrage.prototype
        {  16960u, "Venom" },  // Powers/Player/Venom/WebSplat.prototype
        {  16961u, "Iron Fist" },  // Powers/Player/IronFist/TigerClawStanceBuff.prototype
        {  16963u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveVenom.prototype
        {  16968u, "Venom" },  // Powers/Player/Venom/BigPunchSpender.prototype
        {  16969u, "Luke Cage" },  // Powers/Player/LukeCage/Talents/HeroesForHireHeroesCall.prototype
        {  16973u, "Hulk" },  // Powers/Player/Hulk/Rework/LeapImplodeEnd.prototype
        {  16975u, "Doctor Strange" },  // Powers/Player/DoctorStrange/MysticEnergyOrbVisual6.prototype
        {  16977u, "Iron Fist" },  // Powers/Player/IronFist/TigerClawStanceAccuracySummon.prototype
        {  16978u, "Venom" },  // Powers/Player/Venom/SymbioteDrainPowerGreen3.prototype
        {  16983u, "Luke Cage" },  // Powers/Player/LukeCage/ThrowCarComboPointGainEffect.prototype
        {  16984u, "Blade" },  // Powers/Player/Blade/UnleashGlaiveMissileEffect.prototype
        {  16986u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/JeanGreyPullTowardsPoint.prototype
        {  16987u, "Cyclops" },  // Powers/Player/Cyclops/ChanneledEnergyBeamSlowEffect.prototype
        {  16988u, "Nova" },  // Powers/Player/Nova/Talents/Talent4PulsarChargeDmg.prototype
        {  16989u, "X-23" },  // Powers/Player/X23/MvmtSTSS.prototype
        {  16991u, "Loki" },  // Powers/Player/Loki/LightBeamCommandIllusions.prototype
        {  16992u, "Hulk" },  // Powers/Player/Hulk/Rework/PassiveStrongProcEffectHaste.prototype
        {  16995u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/JessicaJones/JessicaJonesDefaultAttack.prototype
        {  16996u, "Winter Soldier" },  // Powers/Player/WinterSoldier/ArmBlastSecondaryResourceAddFuriousLunge.prototype
        {  16997u, "Iceman" },  // Powers/Player/Iceman/HotspotBeamAsCombo.prototype
        {  17001u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/AcornMeteor3rdHit.prototype
        {  17004u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/GambitRaginCajunProcEffect.prototype
        {  17005u, "Vision" },  // Powers/Player/Vision/DenseModeAnimSwap.prototype
        {  17006u, "Kitty Pryde" },  // Powers/Player/KittyPryde/Talents/Talent3LockheedAutoAoE.prototype
        {  17007u, "Green Goblin" },  // Powers/Player/GreenGoblin/GoblinCannon.prototype
        {  17008u, "Ultron" },  // Powers/Player/Ultron/EncephaloBeam.prototype
        {  17009u, "Human Torch" },  // Powers/Player/HumanTorch/ConsumeCooldownReductionCombo.prototype
        {  17010u, "Ant-Man" },  // Powers/Player/AntMan/Talents/AntRespawnTalent.prototype
        {  17016u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/IronFist/IronFistHealingChi.prototype
        {  17017u, "Moon Knight" },  // Powers/Player/MoonKnight/NunchuckBulldozeHotspotKnockback.prototype
        {  17019u, "Nova" },  // Powers/Player/Nova/PulsarHotspotEffectRR.prototype
        {  17021u, "Angela" },  // Powers/Player/Angela/HybridTreeModRibbon.prototype
        {  17024u, "Storm" },  // Powers/Player/Storm/ZephyrLightningHotspotEffect.prototype
        {  17033u, "Black Panther" },  // Powers/Player/BlackPanther/OctoChargeEffect.prototype
        {  17036u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/CableKineticBarrierSlowEffect.prototype
        {  17039u, "She-Hulk" },  // Powers/Player/SheHulk/DefenseAttorney.prototype
        {  17041u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/BetaRayBillLightningBarrageDoT.prototype
        {  17043u, "Deadpool" },  // Powers/Player/Deadpool/Talents/PowerUpsTalent.prototype
        {  17051u, "Taskmaster" },  // Powers/Player/Taskmaster/StudentsPMCRiotPetShieldPlant.prototype
        {  17052u, "Beast" },  // Powers/Player/Beast/ShieldGadgetOnChainKillSummonLocus.prototype
        {  17053u, "Gambit" },  // Powers/Player/Gambit/Ultimate.prototype
        {  17055u, "Doctor Strange" },  // Powers/Player/DoctorStrange/SeraphimShieldAsProc.prototype
        {  17056u, "Carnage" },  // Powers/Player/Carnage/ClawPummelDamageComboRightSlash.prototype
        {  17057u, "Black Widow" },  // Powers/Player/BlackWidow/Tumble.prototype
        {  17058u, "Thing" },  // Powers/Player/Thing/Rework/DiscusTossMissileEffect.prototype
        {  17065u, "Captain America" },  // Powers/Player/CaptainAmerica/BoomerangThrowSerum.prototype
        {  17069u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BigBeamMissileEffectRed.prototype
        {  17070u, "Black Cat" },  // Powers/Player/BlackCat/ClawTwirlProcEffect.prototype
        {  17071u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/OnslaughtMentalOrbExplosion.prototype
        {  17072u, "She-Hulk" },  // Powers/Player/SheHulk/MoveToStrike.prototype
        {  17073u, "Magneto" },  // Powers/Player/Magneto/ShrapnelAuraRecurringEffect.prototype
        {  17075u, "Cyclops" },  // Powers/Player/Cyclops/Rework/UltimateBeastMelee3.prototype
        {  17078u, "Loki" },  // Powers/Player/Loki/UltimateTransformComboActivate.prototype
        {  17080u, "Green Goblin" },  // Powers/Player/GreenGoblin/MachineGunsTargetAudioCombo.prototype
        {  17081u, "Carnage" },  // Powers/Player/Carnage/BasicClawsAxeWasUsedLast.prototype
        {  17082u, "She-Hulk" },  // Powers/Player/SheHulk/SurpriseWitnessAsCombo.prototype
        {  17084u, "Loki" },  // Powers/Player/Loki/UltimateCharge.prototype
        {  17087u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/IronManPassiveHiddenPassive.prototype
        {  17089u, "Gambit" },  // Powers/Player/Gambit/BoWhirlwindHotspotEffect.prototype
        {  17090u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/JubileeBoom.prototype
        {  17093u, "Rogue" },  // Powers/Player/Rogue/UltimateRaginCajunProcEffect.prototype
        {  17094u, "Ghost Rider" },  // Powers/Player/GhostRider/SpiritofVengeance.prototype
        {  17095u, "Hulk" },  // Powers/Player/Hulk/Rework/DashCrashEndComboVeryAngry.prototype
        {  17096u, "Squirrel Girl" },  // Powers/Player/SquirrelGirl/GoForTheEyesHit3.prototype
        {  17100u, "Kitty Pryde" },  // Powers/Player/KittyPryde/UltimateSteroid.prototype
        {  17105u, "Hawkeye" },  // Powers/Player/Hawkeye/DisengagingShot.prototype
        {  17108u, "Luke Cage" },  // Powers/Player/LukeCage/PetPowers/ColleenWing/BladeDanceStart.prototype
        {  17112u, "Silver Surfer" },  // Powers/Player/SilverSurfer/BlackHoleExplosion.prototype
        {  17113u, "Human Torch" },  // Powers/Player/HumanTorch/Talents/FlameTornado.prototype
        {  17115u, "Punisher" },  // Powers/Player/Punisher/UltimateVengeanceProcEffect.prototype
        {  17116u, "Scarlet Witch" },  // Powers/Player/ScarletWitch/Rework/UnmakeReality.prototype
        {  17118u, "Green Goblin" },  // Powers/Player/GreenGoblin/SummonSonicToads.prototype
        {  17120u, "Juggernaut" },  // Powers/Player/Juggernaut/WrathOfCyttorakMomentumGain.prototype
        {  17128u, "Colossus" },  // Powers/Player/Colossus/InCombatArmorBuff.prototype
        {  17132u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/Talents/SpikedBallSkillshot.prototype
        {  17133u, "Vision" },  // Powers/Player/Vision/NoRobotBuff.prototype
        {  17139u, "Daredevil" },  // Powers/Player/Daredevil/Update/ClubRicochet.prototype
        {  17146u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/ColossusMetalSkinRegenFullProcEffect.prototype
        {  17150u, "Moon Knight" },  // Powers/Player/MoonKnight/RapidFireMissileEffect.prototype
        {  17151u, "Kitty Pryde" },  // Powers/Player/KittyPryde/LockheedChannelFire.prototype
        {  17155u, "Nightcrawler" },  // Powers/Player/Nightcrawler/SwordThrowMovement.prototype
        {  17157u, "Magneto" },  // Powers/Player/Magneto/Talents/DebrisSpenderMoreDebrisPerOrb.prototype
        {  17158u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/LivingLaserLaserBlastShieldThornsProc.prototype
        {  17159u, "Ultron" },  // Powers/Player/Ultron/Talents/Talent3MeleeDrones.prototype
        {  17160u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/PassiveHawkeye.prototype
        {  17164u, "Kitty Pryde" },  // Powers/Player/KittyPryde/TagTeamFireBreath.prototype
        {  17165u, "Loki" },  // Powers/Player/Loki/ChainBoltChainCombo.prototype
        {  17167u, "Emma Frost" },  // Powers/Player/EmmaFrost/Update/TelepathyHiddenPassive.prototype
        {  17168u, "Dr. Doom" },  // Powers/Player/DrDoom/BasicPunch.prototype
        {  17169u, "Emma Frost" },  // Powers/Player/EmmaFrost/TelepathicIllusion.prototype
        {  17172u, "Venom" },  // Powers/Player/Venom/FuriousLunge.prototype
        {  17174u, "Emma Frost" },  // Powers/Player/EmmaFrost/DiamondKneeComboMoveSpeedBuff.prototype
        {  17178u, "Magneto" },  // Powers/Player/Magneto/HomingBlast.prototype
        {  17179u, "Nick Fury" },  // Powers/Player/NickFury/DangerClose.prototype
        {  17180u, "Cyclops" },  // Powers/Player/Cyclops/Talents/CallinProcTalent.prototype
        {  17181u, "Venom" },  // Powers/Player/Venom/UltimateSymbioteDrainPower2.prototype
        {  17182u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/WolverineBasicRonin.prototype
        {  17186u, "Invisible Woman" },  // Powers/Player/InvisibleWoman/ImplodeExplodeInCombo.prototype
        {  17187u, "Punisher" },  // Powers/Player/Punisher/Talents/LighterMags.prototype
        {  17188u, "Iron Man" },  // Powers/Player/IronMan/MicrolasersHotspotEffect.prototype
        {  17189u, "Hawkeye" },  // Powers/Player/Hawkeye/Talents/SpeedLoaderPiercingTalent.prototype
        {  17190u, "Rogue" },  // Powers/Player/Rogue/StolenPowers/IronFistDragonStanceUppercutCharge.prototype
        {  17191u, "Nightcrawler" },  // Powers/Player/Nightcrawler/BamfFrenzy.prototype
    };

    /// <summary>True when the given power enum index is known to belong to a specific shipping
    /// hero — i.e. <see cref="Names"/> contains it.</summary>
    public static bool TryGetHero(uint powerPrototypeEnumIndex, out string heroDisplayName)
    {
        if (Names.TryGetValue(powerPrototypeEnumIndex, out string? hit))
        {
            heroDisplayName = hit;
            return true;
        }
        heroDisplayName = string.Empty;
        return false;
    }
}
