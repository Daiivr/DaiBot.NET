using PKHeX.Core;
using SysBot.Base;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SysBot.Pokemon;

public class RaidSettings : IBotStateSettings, ICountSettings
{
    private const string Counts = nameof(Counts);

    private const string FeatureToggle = nameof(FeatureToggle);

    private const string Hosting = nameof(Hosting);

    private int _completedRaids;

    [Category(Counts), Description("Raids iniciadas")]
    public int CompletedRaids
    {
        get => _completedRaids;
        set => _completedRaids = value;
    }

    [Category(FeatureToggle), Description("Hace eco a cada miembro del grupo mientras se fijan en un Pokémon.")]
    public bool EchoPartyReady { get; set; }

    [Category(Counts), Description("Cuando esté habilitado, los recuentos se emitirán cuando se solicite una verificación de estado.")]
    public bool EmitCountsOnStatusCheck { get; set; }

    [Category(FeatureToggle), Description("Permite que el bot repita su código de amigo si está configurado.")]
    public string FriendCode { get; set; } = string.Empty;

    [Category(Hosting), Description("Número de incursiones que se deben realizar antes de intentar agregar o eliminar amigos. Establecer un valor de 1 le indicará al bot que organice una incursión y luego comience a agregar o eliminar amigos.")]
    public int InitialRaidsToHost { get; set; }

    [Category(Hosting), Description("Código de enlace máximo para organizar la incursión. Establezca esto en -1 para alojar sin código.")]
    public int MaxRaidCode { get; set; } = 8199;

    [Category(Hosting), Description("Código de enlace mínimo para organizar la incursión. Establezca esto en -1 para alojar sin código.")]
    public int MinRaidCode { get; set; } = 8180;

    [Category(Hosting), Description("Número de solicitudes de amistad para aceptar cada vez.")]
    public int NumberFriendsToAdd { get; set; }

    [Category(Hosting), Description("Número de amigos para eliminar cada vez.")]
    public int NumberFriendsToDelete { get; set; }

    [Category(Hosting), Description("El perfil de Nintendo Switch que estás usando para administrar amigos. Por ejemplo, configúrelo en 2 si está utilizando el segundo perfil.")]
    public int ProfileNumber { get; set; } = 1;

    [Category(FeatureToggle), Description("Descripción opcional de la incursión que realiza el bot. Utiliza la detección automática de Pokémon si se deja en blanco.")]
    public string RaidDescription { get; set; } = string.Empty;

    [Category(Hosting), Description("Número de incursiones que se realizarán entre cada intento de agregar amigos.")]
    public int RaidsBetweenAddFriends { get; set; }

    [Category(Hosting), Description("Número de redadas a realizar entre intentos de eliminar amigos.")]
    public int RaidsBetweenDeleteFriends { get; set; }

    [Category(Hosting), Description("Número de fila para empezar a intentar agregar amigos.")]
    public int RowStartAddingFriends { get; set; } = 1;

    [Category(Hosting), Description("Número de fila para empezar a intentar eliminar amigos.")]
    public int RowStartDeletingFriends { get; set; } = 1;

    [Category(FeatureToggle), Description("Cuando está habilitado, la pantalla se apagará durante la operación normal del bucle del bot para ahorrar energía.")]
    public bool ScreenOff { get; set; }

    [Category(Hosting), Description("Número de segundos que se deben esperar antes de intentar iniciar una redada. Varía de 0 a 180 segundos.")]
    public int TimeToWait { get; set; } = 90;

    public int AddCompletedRaids() => Interlocked.Increment(ref _completedRaids);

    public IEnumerable<string> GetNonZeroCounts()
    {
        if (!EmitCountsOnStatusCheck)
            yield break;
        if (CompletedRaids != 0)
            yield return $"Incursiones iniciadas: {CompletedRaids}";
    }

    /// <summary>
    /// Gets a random trade code based on the range settings.
    /// </summary>
    public int GetRandomRaidCode() => Util.Rand.Next(MinRaidCode, MaxRaidCode + 1);

    public override string ToString() => "Configuración del Bot de incursión";
}
