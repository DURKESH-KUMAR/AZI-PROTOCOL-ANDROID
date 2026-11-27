using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;
using Photon.Realtime;
using PhantomWarrior.Auth;

/// <summary>
/// Photon Custom Authentication for the game's Photon app (the only Photon app it uses:
/// PhotonServerSettings has no separate Chat / Voice / Fusion App IDs).
///
/// Before every connect the player signs in to Unity Authentication and the Unity-signed
/// access token is sent to Photon as POST data. Photon forwards it to the photon-auth worker
/// (Server/photon-auth-worker), which verifies the signature and returns the player's id.
/// With "allow anonymous clients" turned OFF in the Photon dashboard, a client that only knows
/// the App ID can no longer connect.
/// </summary>
public static class PhotonAuth
{
    /// <summary>Unity Authentication available and expected to be used.</summary>
    public static bool Enabled => PlayerAccount.Supported;

    public static string LastError => PlayerAccount.LastError;

    /// <summary>
    /// Signs in (or refreshes the token) and sets <see cref="PhotonNetwork.AuthValues"/>.
    /// Returns false if sign-in failed (see <see cref="LastError"/>).
    /// </summary>
    public static async Task<bool> PrepareAsync()
    {
        if (!await PlayerAccount.EnsureSignedInAsync())
            return false;

        AuthenticationValues values = new AuthenticationValues(PlayerAccount.PlayerId)
        {
            AuthType = CustomAuthenticationType.Custom,
        };
        // POST body instead of URL query: the token never ends up in server access logs.
        values.SetAuthPostData(new Dictionary<string, object>
        {
            { "token", PlayerAccount.AccessToken },
            { "userId", PlayerAccount.PlayerId },
        });
        PhotonNetwork.AuthValues = values;
        return true;
    }

    /// <summary>Photon rejected the token (expired / revoked): force a fresh one next time.</summary>
    public static void OnRejected()
    {
        PlayerAccount.InvalidateToken();
    }
}
