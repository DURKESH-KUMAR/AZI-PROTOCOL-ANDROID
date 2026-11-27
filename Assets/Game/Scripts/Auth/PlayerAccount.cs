using System;
using System.Threading.Tasks;
using UnityEngine;
#if PW_UGS_AUTH
using Unity.Services.Authentication;
using Unity.Services.Core;
#endif

namespace PhantomWarrior.Auth
{
    /// <summary>
    /// The player's account, backed by Unity Authentication (Unity Gaming Services).
    /// Signs in silently (anonymous account, kept on the device via Unity's session token, so
    /// the same PlayerId comes back every launch) and provides a short-lived, Unity-signed
    /// access token. That token is what Photon's Custom Authentication checks (via the
    /// photon-auth worker), so the Photon App ID alone is useless to other clients.
    ///
    /// Lives in its own assembly: the real code only compiles once the
    /// com.unity.services.authentication package is installed (PW_UGS_AUTH version define),
    /// so a missing package can never break the game's build.
    /// </summary>
    public static class PlayerAccount
    {
        /// <summary>Unity access tokens last 1 hour; refresh well before that.</summary>
        static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(45);

        static DateTime tokenTime = DateTime.MinValue;
        static Task<bool> pending;

        /// <summary>True when the Unity Authentication package is installed.</summary>
        public static bool Supported
        {
            get
            {
#if PW_UGS_AUTH
                return true;
#else
                return false;
#endif
            }
        }

        public static string PlayerId { get; private set; } = "";
        public static string AccessToken { get; private set; } = "";
        public static string LastError { get; private set; } = "";

        public static bool HasValidToken => !string.IsNullOrEmpty(AccessToken) && DateTime.UtcNow - tokenTime < TokenLifetime;

        /// <summary>
        /// Signs in (or refreshes an old token). Safe to call repeatedly; concurrent calls
        /// share one request. Returns false with <see cref="LastError"/> set on failure.
        /// </summary>
        public static Task<bool> EnsureSignedInAsync()
        {
            if (HasValidToken)
                return Task.FromResult(true);
            if (pending != null && !pending.IsCompleted)
                return pending;
            pending = SignInAsync();
            return pending;
        }

        /// <summary>Drop the cached token (e.g. Photon rejected it) so the next call fetches a new one.</summary>
        public static void InvalidateToken()
        {
            AccessToken = "";
            tokenTime = DateTime.MinValue;
        }

        static async Task<bool> SignInAsync()
        {
#if PW_UGS_AUTH
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                    await UnityServices.InitializeAsync();
                else
                    while (UnityServices.State == ServicesInitializationState.Initializing)
                        await Task.Yield();

                IAuthenticationService auth = AuthenticationService.Instance;
                // Signing in again with the stored session token returns the SAME player and a
                // fresh access token, which is also how an expiring token is refreshed.
                if (!auth.IsSignedIn || auth.IsExpired || DateTime.UtcNow - tokenTime >= TokenLifetime)
                {
                    if (auth.IsSignedIn)
                        auth.SignOut(false); // keep the session token -> same PlayerId
                    await auth.SignInAnonymouslyAsync();
                }

                PlayerId = auth.PlayerId ?? "";
                AccessToken = auth.AccessToken ?? "";
                tokenTime = DateTime.UtcNow;
                LastError = "";
                if (string.IsNullOrEmpty(AccessToken))
                {
                    LastError = "Signed in but no access token was issued.";
                    return false;
                }
                return true;
            }
            catch (AuthenticationException e)
            {
                LastError = "Sign-in failed: " + e.Message;
            }
            catch (RequestFailedException e)
            {
                LastError = "Sign-in service unreachable: " + e.Message;
            }
            catch (Exception e)
            {
                // Unity Services wraps the real reason (e.g. project not linked) in inner exceptions.
                Exception root = e;
                while (root.InnerException != null)
                    root = root.InnerException;
                LastError = root == e ? "Sign-in error: " + e.Message : "Sign-in error: " + root.Message;
                if (e.Message.Contains("couldn't be initialized"))
                    LastError += "\n(Is the project linked in Edit > Project Settings > Services?)";
            }
            Debug.LogWarning("[PlayerAccount] " + LastError);
            InvalidateToken();
            return false;
#else
            LastError = "Unity Authentication package (com.unity.services.authentication) is not installed.";
            await Task.CompletedTask;
            return false;
#endif
        }
    }
}
