using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SysBot.Pokemon;

public class TradeSettings : IBotStateSettings, ICountSettings
{
    private const string CountStats = nameof(CountStats);
    private const string HOMELegality = nameof(HOMELegality);
    private const string TradeConfig = nameof(TradeConfig);
    private const string VGCPastesConfig = nameof(VGCPastesConfig);
    private const string Miscellaneous = nameof(Miscellaneous);
    private const string RequestFolders = nameof(RequestFolders);
    private const string EmbedSettings = nameof(EmbedSettings);
    public override string ToString() => "Ajustes de configuración de Trade";

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class EmojiInfo
    {
        [Description("The full string for the emoji.")]
        public string EmojiString { get; set; } = string.Empty;

        public override string ToString()
        {
            return string.IsNullOrEmpty(EmojiString) ? "No establecido" : EmojiString;
        }
    }

    [Category(TradeConfig), Description("Ajustes relacionados con la configuración del trade."), DisplayName("Configuración del trade"), Browsable(true)]
    public TradeSettingsCategory TradeConfiguration { get; set; } = new();

    [Category(VGCPastesConfig), Description("Ajustes relacionados con la Configuración de VGCPastes."),DisplayName("Configuración de VGCPastes"), Browsable(true)]
    public VGCPastesCategory VGCPastesConfiguration { get; set; } = new();

    [Category(EmbedSettings), Description("Ajustes relacionados con el Trade Embed en Discord."), DisplayName("Configuración del Embed Trade"), Browsable(true)]
    public TradeEmbedSettingsCategory TradeEmbedSettings { get; set; } = new();

    [Category(HOMELegality), Description("Ajustes relacionados con la legalidad de HOME."), DisplayName("Configuración de la Legalidad de HOME"), Browsable(true)]
    public HOMELegalitySettingsCategory HomeLegalitySettings { get; set; } = new();

    [Category(RequestFolders), Description("Ajustes relacionados con las carpetas de solicitud."), DisplayName("Configuración de las carpetas de solicitud."), Browsable(true)]
    public RequestFolderSettingsCategory RequestFolderSettings { get; set; } = new();

    [Category(CountStats), Description("Ajustes relacionados con las estadísticas de recuento de trades."), DisplayName("Configuración de las estadísticas de recuento de trades"), Browsable(true)]
    public CountStatsSettingsCategory CountStatsSettings { get; set; } = new();


    [Category(TradeConfig), TypeConverter(typeof(CategoryConverter<TradeSettingsCategory>))]
    public class TradeSettingsCategory
    {
        public override string ToString() => "Trade Configuration Settings";

        [Category(TradeConfig), Description("Código de enlace mínimo."), DisplayName("Código mínimo de enlace comercial")]
        public int MinTradeCode { get; set; } = 0;

        [Category(TradeConfig), Description("Código de enlace máximo."), DisplayName("Código de enlace comercial máximo")]
        public int MaxTradeCode { get; set; } = 9999_9999;

        [Category(TradeConfig), Description("Si se establece en True, el código de trade de los usuarios de Discord se almacenará y se utilizará repetidamente sin cambiar."), DisplayName("Almacenar y reutilizar códigos de Trade")]
        public bool StoreTradeCodes { get; set; } = false;

        [Category(TradeConfig), Description("Tiempo a esperar por un usuario en segundos."), DisplayName("Tiempo a esperar por un usuario (segundos)")]
        public int TradeWaitTime { get; set; } = 30;

        [Category(TradeConfig), Description("Cantidad máxima de tiempo en segundos pulsando A para esperar a que se procese una operación."), DisplayName("Tiempo máximo de confirmación de la operación (segundos)")]
        public int MaxTradeConfirmTime { get; set; } = 25;

        [Category(TradeConfig), Description("Seleccione la especie por defecto para \"ItemTrade\", si está configurado."), DisplayName("Especies por defecto para el Item Trade")]
        public Species ItemTradeSpecies { get; set; } = Species.None;

        [Category(TradeConfig), Description("Item por defecto que se enviará si no se especifica ninguno."), DisplayName("Item por defecto para trades")]
        public HeldItem DefaultHeldItem { get; set; } = HeldItem.None;

        [Category(TradeConfig), Description("Si se establece en True, cada Pokemon válido vendrá con todos los Movimientos Reaprendibles sugeridos sin necesidad de utilizar un batch command."), DisplayName("Sugerir movimientos reaprendibles por defecto")]
        public bool SuggestRelearnMoves { get; set; } = true;

        [Category(TradeConfig), Description("If set to True, each showdown set will go through a spell check first to make sure species name is correct."), DisplayName("Habilitar revisión ortográfica?")]
        public bool SpellCheck { get; set; } = true;

        [Category(TradeConfig), Description("Activar o desactivar los trades por lotes."), DisplayName("Permitir trades por lotes")]
        public bool AllowBatchTrades { get; set; } = true;

        [Category(TradeConfig), Description("Máximo de pokemons de un solo comercio. El modo por lotes se cerrará si esta configuración es inferior a 1"), DisplayName("Máximo de Pokémon por trades")]
        public int MaxPkmsPerTrade { get; set; } = 1;

        [Category(TradeConfig), Description("Dump Trade: La rutina de dump se detendrá tras un número máximo de deumps de un mismo usuario."), DisplayName("Dumps máximos por operación")]
        public int MaxDumpsPerTrade { get; set; } = 20;

        [Category(TradeConfig), Description("Dump Trade: La rutina de dump se detendrá después de pasar x segundos en el trade."), DisplayName("Tiempo máximo de dump (segundos)")]
        public int MaxDumpTradeTime { get; set; } = 180;

        [Category(TradeConfig), Description("Dump Trade: Si está activada, la rutina de dump mostrará al usuario información sobre la comprobación de la legalidad."), DisplayName("Verificación de la legalidad del dumping")]
        public bool DumpTradeLegalityCheck { get; set; } = true;

        [Category(TradeConfig), Description("Ajuste LGPE.")]
        public int TradeAnimationMaxDelaySeconds = 25;

        public enum HeldItem
        {
            None = 0,
            AbilityPatch = 1606,
            RareCandy = 50,
            AbilityCapsule = 645,
            BottleCap = 795,
            expCandyL = 1127,
            expCandyXL = 1128,
            MasterBall = 1,
            Nugget = 92,
            BigPearl = 89,
            GoldBottleCap = 796,
            ppUp = 51,
            ppMax = 53,
            FreshStartMochi = 2479,
        }
    }

    [Category(EmbedSettings), TypeConverter(typeof(CategoryConverter<TradeEmbedSettingsCategory>))]
    public class TradeEmbedSettingsCategory
    {
        public override string ToString() => "Ajustes de configuración de Trade Embed";

        private bool _useEmbeds;
        [Category(EmbedSettings), Description("Si es verdadero, mostrará hermosos embeds en sus canales de trade de discord de lo que el usuario este tradeando. False mostrará el texto por defecto."), DisplayName("Usar Embeds")]
        public bool UseEmbeds
        {
            get => _useEmbeds;
            set
            {
                _useEmbeds = value;
                OnUseEmbedsChanged();
            }
        }

        private void OnUseEmbedsChanged()
        {
            if (!_useEmbeds)
            {
                PreferredImageSize = ImageSize.Size256x256;
                MoveTypeEmojis = false;
                ShowScale = false;
                ShowTeraType = false;
                ShowLevel = false;
                ShowMetDate = false;
                ShowAbility = false;
                ShowNature = false;
                ShowIVs = false;
            }
        }

        [Category(EmbedSettings), Description("Tamaño preferido de la imagen de la especie para embeds."), DisplayName("Tamaño de la imagen del Pokémon")]
        public ImageSize PreferredImageSize { get; set; } = ImageSize.Size256x256;

        [Category(TradeConfig), Description("URL del título de Emebed."), DisplayName("URL del título del Embed")]
        public string TradingBotUrl { get; set; } = "";

        [Category(EmbedSettings), Description("Mostrará los iconos de tipo de movimiento junto a los movimientos en el Embed Trade (sólo Discord). Requiere que el usuario suba los emojis a su servidor."), DisplayName("¿Mostrar Emojis de Movimientos?")]
        public bool MoveTypeEmojis { get; set; } = true;

        [Category(EmbedSettings), Description("Mostrará los iconos de Tera Tipo junto a los movimientos en el Embed Trade (sólo Discord). Requiere que el usuario suba los emojis a su servidor."), DisplayName("¿Mostrar Emojis de Tipo Tera?")]
        public bool UseTeraEmojis { get; set; } = true;

        [Category(EmbedSettings), Description("Si es verdadero, se mostrarán los emojis para las escalas XXXS y XXXL en el Embed Trade."), DisplayName("¿Usar Emojis de Escala?")]
        public bool UseScaleEmojis { get; set; } = true; // Habilitado por defecto.

        [Category(EmbedSettings), Description("Información personalizada de Emoji para los tipos de movimiento."), DisplayName("Emojis de Movimientos")]
        public List<MoveTypeEmojiInfo> CustomTypeEmojis { get; set; } = new List<MoveTypeEmojiInfo>
        {
            new(MoveType.Bug),
            new(MoveType.Fire),
            new(MoveType.Flying),
            new(MoveType.Ground),
            new(MoveType.Water),
            new(MoveType.Grass),
            new(MoveType.Ice),
            new(MoveType.Rock),
            new(MoveType.Ghost),
            new(MoveType.Steel),
            new(MoveType.Fighting),
            new(MoveType.Electric),
            new(MoveType.Dragon),
            new(MoveType.Psychic),
            new(MoveType.Dark),
            new(MoveType.Normal),
            new(MoveType.Poison),
            new(MoveType.Fairy),
        };

        [Category(EmbedSettings), TypeConverter(typeof(ExpandableObjectConverter)), Description("Configuración de emojis para todos los tipos Tera, incluyendo 'Stellar'."), DisplayName("Emojis de Tipo Tera")]
        public class TeraTypeEmojisSettings
        {
            public override string ToString() => "(Collection)";

            [Description("Emoji para los tipos Tera estándar."), DisplayName("Emojis de Tipo Tera")]
            public List<TeraTypeEmojiInfo> TeraTypeEmojis { get; set; } = new List<TeraTypeEmojiInfo>
            {
                new(MoveType.Bug),
                new(MoveType.Fire),
                new(MoveType.Flying),
                new(MoveType.Ground),
                new(MoveType.Water),
                new(MoveType.Grass),
                new(MoveType.Ice),
                new(MoveType.Rock),
                new(MoveType.Ghost),
                new(MoveType.Steel),
                new(MoveType.Fighting),
                new(MoveType.Electric),
                new(MoveType.Dragon),
                new(MoveType.Psychic),
                new(MoveType.Dark),
                new(MoveType.Normal),
                new(MoveType.Poison),
                new(MoveType.Fairy)
            };

            [Description("Emoji para el tipo Tera 'Stellar'."), DisplayName("Emoji Tera Tipo Stellar")]
            public EmojiInfo StellarTeraTypeEmoji { get; set; } = new EmojiInfo();
        }
        public TeraTypeEmojisSettings TeraEmojis { get; set; } = new TeraTypeEmojisSettings(); // Instancia de la configuración de emojis de tipo Tera.

        [Category(EmbedSettings), TypeConverter(typeof(ExpandableObjectConverter)), Description("Configuración de emojis para las escalas XXXS y XXXL."), DisplayName("Emojis de tamaño")]
        public class ScaleEmojisSettings
        {
            public override string ToString() => "(Collection)";

            [Description("Emoji para la escala XXXS."), DisplayName("Emoji Escala XXXS")]
            public EmojiInfo ScaleXXXSEmoji { get; set; } = new EmojiInfo();

            [Description("Emoji para la escala XXXL."), DisplayName("Emoji Escala XXXL")]
            public EmojiInfo ScaleXXXLEmoji { get; set; } = new EmojiInfo();
        }
        public ScaleEmojisSettings ScaleEmojis { get; set; } = new ScaleEmojisSettings(); // Instancia de la configuración de emojis de escala.

        [Category(EmbedSettings), TypeConverter(typeof(ExpandableObjectConverter)), Description("Configuración de emojis para Pokémon Shiny."), DisplayName("Emojis Shiny")]
        public class ShinyEmojisSettings
        {
            public override string ToString() => "(Collection)";

            [Description("Emoji para Pokémon con Shiny Square."), DisplayName("Emoji Shiny Square")]
            public EmojiInfo ShinySquareEmoji { get; set; } = new EmojiInfo();

            [Description("Emoji para Pokémon Shiny normal."), DisplayName("Emoji Shiny Normal")]
            public EmojiInfo ShinyNormalEmoji { get; set; } = new EmojiInfo();
        }
        public ShinyEmojisSettings ShinyEmojis { get; set; } = new ShinyEmojisSettings(); // Instancia de la configuración de emojis Shiny.

        [Category(EmbedSettings), TypeConverter(typeof(ExpandableObjectConverter)), Description("Configuración de emojis para géneros."), DisplayName("Emojis de Género")]
        public class GenderEmojisSettings
        {
            public override string ToString() => "(Collection)";

            [Description("La cadena completa para el emoji de género masculino."), DisplayName("Emoji Masculino")]
            public EmojiInfo MaleEmoji { get; set; } = new EmojiInfo();

            [Description("La cadena completa para el emoji de género femenino."), DisplayName("Emoji Femenino")]
            public EmojiInfo FemaleEmoji { get; set; } = new EmojiInfo();
        }
        public GenderEmojisSettings GenderEmojis { get; set; } = new GenderEmojisSettings(); // Instancia de la configuración de emojis de género.

        [Category(EmbedSettings), TypeConverter(typeof(ExpandableObjectConverter)), Description("Configuración de emojis para marcas especiales y estados."), DisplayName("Emojis de Marcas y Estados Especiales")]
        public class SpecialMarksEmojisSettings
        {
            public override string ToString() => "(Collection)";

            [Description("La información del emoji para mostrar el estado del regalo misterioso."), DisplayName("Emoji Regalo Misterioso")]
            public EmojiInfo MysteryGiftEmoji { get; set; } = new EmojiInfo();

            [Description("La información del emoji para mostrar la marca alfa."), DisplayName("Emoji Marca Alfa")]
            public EmojiInfo AlphaMarkEmoji { get; set; } = new EmojiInfo();

            [Description("La información emoji para mostrar la marca Imbatible."), DisplayName("Emoji Imbatible")]
            public EmojiInfo MightiestMarkEmoji { get; set; } = new EmojiInfo();

            [Description("La información emoji para mostrar el emoji alfa en Legends: Arceus."), DisplayName("Emoji Alfa PLA")]
            public EmojiInfo AlphaPLAEmoji { get; set; } = new EmojiInfo();
        }
        public SpecialMarksEmojisSettings SpecialMarksEmojis { get; set; } = new SpecialMarksEmojisSettings(); // Instancia de la configuración de emojis de marcas y estados especiales.


        [Category(EmbedSettings), Description("Se mostrará la Escala en el Embed Trade (SV y Discord solamente). Requiere que el usuario suba los emojis a su servidor."), DisplayName("Mostrar Tamaño")]
        public bool ShowScale { get; set; } = true;

        [Category(EmbedSettings), Description("Mostrará el Tera Tipo en el Embed Trade (sólo SV y Discord)."), DisplayName("Mostrar Tera Tipo")]
        public bool ShowTeraType { get; set; } = true;

        [Category(EmbedSettings), Description("Se mostrará el nivel en el Embed Trade (Discord solamente)."), DisplayName("Mostrar Nivel")]
        public bool ShowLevel { get; set; } = true;

        [Category(EmbedSettings), Description("Mostrará MetDate en el Embed Trade (sólo Discord)."), DisplayName("Mostrar Fecha de Encuentro")]
        public bool ShowMetDate { get; set; } = true;

        [Category(EmbedSettings), Description("Se mostrará Habilidad en el Embed Trade (Discord solamente)."), DisplayName("Mostrar Habilidad")]
        public bool ShowAbility { get; set; } = true;

        [Category(EmbedSettings), Description("Se mostrará la naturaleza en el Embed Trade (Discord solamente)."), DisplayName("Mostrar Naturaleza")]
        public bool ShowNature { get; set; } = true;

        [Category(EmbedSettings), Description("Mostrará IVs en el Embed Trade (Discord solamente)."), DisplayName("Mostrar IVs")]
        public bool ShowIVs { get; set; } = true;

        [Category(EmbedSettings), Description("Mostrará los EVs en el Embed Trade (sólo Discord)."), DisplayName("Mostrar EVs")]
        public bool ShowEVs { get; set; } = true;
    }

    [Category(VGCPastesConfig), TypeConverter(typeof(CategoryConverter<VGCPastesCategory>))]
    public class VGCPastesCategory
    {
        public override string ToString() => "Ajustes de configuración de VGCPastes";

        [Category(VGCPastesConfig), Description("Permitir a los usuarios solicitar y generar equipos utilizando la hoja de cálculo VGCPastes."), DisplayName("Permitir solicitudes VGCPaste")]
        public bool AllowRequests { get; set; } = true;

        [Category(VGCPastesConfig), Description("GID de la pestaña de la hoja de cálculo de la que desea extraer datos.  Sugerencia: https://docs.google.com/spreadsheets/d/ID/gid=1837599752"), DisplayName("GID de la hoja de cálculo")]
        public int GID { get; set; } = 1837599752; // Reg F Tab
    }

    [Category(HOMELegality), TypeConverter(typeof(CategoryConverter<HOMELegalitySettingsCategory>))]
    public class HOMELegalitySettingsCategory
    {
        public override string ToString() => "Ajustes de la legalidad de HOME";

        [Category(HOMELegality), Description("Impide intercambiar Pokémon que requieran un Rastreador HOME, aunque el archivo ya lo tenga."), DisplayName("Prohibir Pokémon no nativos")]
        public bool DisallowNonNatives { get; set; } = false;

        [Category(HOMELegality), Description("Impide intercambiar Pokémon que ya tengan un Rastreador HOME."), DisplayName("No permitir Pokémon con rastreador HOME")]
        public bool DisallowTracked { get; set; } = false;
    }

    [Category(RequestFolders), TypeConverter(typeof(CategoryConverter<RequestFolderSettingsCategory>))]
    public class RequestFolderSettingsCategory
    {
        public override string ToString() => "Configuración de las carpetas de solicitud";

        [Category("RequestFolders"), Description("Ruta a su carpeta de eventos. Crea una nueva carpeta llamada 'eventos' y copia la ruta aquí."), DisplayName("Ruta de la carpeta de eventos")]
        public string EventsFolder { get; set; } = string.Empty;

        [Category("RequestFolders"), Description("Ruta a tu carpeta BattleReady. Crea una nueva carpeta llamada 'battleready' y copia la ruta aquí."), DisplayName("Ruta de la carpeta Battle-Ready")]
        public string BattleReadyPKMFolder { get; set; } = string.Empty;
    }

    [Category(Miscellaneous), Description("Ajustes varios"), DisplayName("Miscellaneous")]
    public bool ScreenOff { get; set; } = false;

    /// <summary>
    /// Gets a random trade code based on the range settings.
    /// </summary>
    public int GetRandomTradeCode() => Util.Rand.Next(TradeConfiguration.MinTradeCode, TradeConfiguration.MaxTradeCode + 1);
    public static List<Pictocodes> GetRandomLGTradeCode(bool randomtrade = false)
    {
        var lgcode = new List<Pictocodes>();
        if (randomtrade)
        {
            for (int i = 0; i <= 2; i++)
            {
                // code.Add((pictocodes)Util.Rand.Next(10));
                lgcode.Add(Pictocodes.Pikachu);

            }
        }
        else
        {
            for (int i = 0; i <= 2; i++)
            {
                lgcode.Add((Pictocodes)Util.Rand.Next(10));
                // code.Add(pictocodes.Pikachu);

            }
        }
        return lgcode;
    }


    [Category(CountStats), TypeConverter(typeof(CategoryConverter<CountStatsSettingsCategory>))]
    public class CountStatsSettingsCategory
    {
        public override string ToString() => "Estadísticas del recuento de trades";

        private int _completedSurprise;
        private int _completedDistribution;
        private int _completedTrades;
        private int _completedSeedChecks;
        private int _completedClones;
        private int _completedDumps;
        private int _completedFixOTs;

        [Category(CountStats), Description("Trades sorpresas finalizados")]
        public int CompletedSurprise
        {
            get => _completedSurprise;
            set => _completedSurprise = value;
        }

        [Category(  ), Description("Trades de enlaces finalizados (distribución)")]
        public int CompletedDistribution
        {
            get => _completedDistribution;
            set => _completedDistribution = value;
        }

        [Category(CountStats), Description("Trade de enlace completados (usuario específico)")]
        public int CompletedTrades
        {
            get => _completedTrades;
            set => _completedTrades = value;
        }

        [Category(CountStats), Description("Trades FixOT completados (Usuario específico)")]
        public int CompletedFixOTs
        {
            get => _completedFixOTs;
            set => _completedFixOTs = value;
        }

        [Browsable(false)]
        [Category(CountStats), Description("Trades de control de semillas finalizadas")]
        public int CompletedSeedChecks
        {
            get => _completedSeedChecks;
            set => _completedSeedChecks = value;
        }

        [Category(CountStats), Description("Trades de clonacion completados (usuario específico)")]
        public int CompletedClones
        {
            get => _completedClones;
            set => _completedClones = value;
        }

        [Category(CountStats), Description("Trades de Dumps finalizados (usuario específico)")]
        public int CompletedDumps
        {
            get => _completedDumps;
            set => _completedDumps = value;
        }

        [Category(CountStats), Description("Si se activa, los recuentos se emitirán cuando se solicite una comprobación de estado.")]
        public bool EmitCountsOnStatusCheck { get; set; }

        public void AddCompletedTrade() => Interlocked.Increment(ref _completedTrades);
        public void AddCompletedSeedCheck() => Interlocked.Increment(ref _completedSeedChecks);
        public void AddCompletedSurprise() => Interlocked.Increment(ref _completedSurprise);
        public void AddCompletedDistribution() => Interlocked.Increment(ref _completedDistribution);
        public void AddCompletedDumps() => Interlocked.Increment(ref _completedDumps);
        public void AddCompletedClones() => Interlocked.Increment(ref _completedClones);
        public void AddCompletedFixOTs() => Interlocked.Increment(ref _completedFixOTs);

        public IEnumerable<string> GetNonZeroCounts()
        {
            if (!EmitCountsOnStatusCheck)
                yield break;
            if (CompletedSeedChecks != 0)
                yield return $"Seed Check Trades: {CompletedSeedChecks}";
            if (CompletedClones != 0)
                yield return $"Clone Trades: {CompletedClones}";
            if (CompletedDumps != 0)
                yield return $"Dump Trades: {CompletedDumps}";
            if (CompletedTrades != 0)
                yield return $"Link Trades: {CompletedTrades}";
            if (CompletedDistribution != 0)
                yield return $"Distribution Trades: {CompletedDistribution}";
            if (CompletedFixOTs != 0)
                yield return $"FixOT Trades: {CompletedFixOTs}";
            if (CompletedSurprise != 0)
                yield return $"Surprise Trades: {CompletedSurprise}";
        }
    }

    [Description("Controla si los recuentos de estadísticas de operaciones se emiten durante las comprobaciones de estado."), DisplayName("Emitir Recuentos al Comprobar Estado")]
    public bool EmitCountsOnStatusCheck
    {
        get => CountStatsSettings.EmitCountsOnStatusCheck;
        set => CountStatsSettings.EmitCountsOnStatusCheck = value;
    }

    public IEnumerable<string> GetNonZeroCounts()
    {
        // Delegating the call to CountStatsSettingsCategory
        return CountStatsSettings.GetNonZeroCounts();
    }

    public class CategoryConverter<T> : TypeConverter
    {
        public override bool GetPropertiesSupported(ITypeDescriptorContext? context) => true;

        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext? context, object value, Attribute[]? attributes) => TypeDescriptor.GetProperties(typeof(T));

        public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType != typeof(string) && base.CanConvertTo(context, destinationType);
    }

    public enum ImageSize
    {
        Size256x256,
        Size128x128
    }

    public class MoveTypeEmojiInfo
    {
        [Description("The type of move.")]
        public MoveType MoveType { get; set; }

        [Description("The Discord emoji string for this move type.")]
        public string EmojiCode { get; set; }

        public MoveTypeEmojiInfo() { }

        public MoveTypeEmojiInfo(MoveType moveType)
        {
            MoveType = moveType;
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(EmojiCode))
                return MoveType.ToString();

            return $"{EmojiCode}";
        }
    }

    public class TeraTypeEmojiInfo
    {
        [Description("The Tera Type.")]
        public MoveType MoveType { get; set; }

        [Description("The Discord emoji string for this tera type.")]
        public string EmojiCode { get; set; }

        public TeraTypeEmojiInfo() { }

        public TeraTypeEmojiInfo(MoveType teraType)
        {
            MoveType = teraType;
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(EmojiCode))
                return MoveType.ToString();

            return $"{EmojiCode}";
        }
    }
}
