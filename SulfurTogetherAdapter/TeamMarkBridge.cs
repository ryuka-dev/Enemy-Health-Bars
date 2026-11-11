using System;
using System.Runtime.CompilerServices;
using SULFURTogether.Api;

namespace EnemyHealthBars.SulfurTogether
{
    /// <summary>
    /// Translates SULFUR Together's team marks (combat API 2) into Enemy Health Bars' own terms; nothing else
    /// sees a SULFURTogether.Api type.
    ///
    /// The bars read the number of other players' marks on an enemy from <see cref="NetTeamMarks"/>, read
    /// again when it reports a change, and register as a mark presenter: while a bar shows an enemy's marks,
    /// SULFUR Together does not draw its icon over that enemy. A faded, hidden or out-of-range bar hands the
    /// marks back to the icon.
    ///
    /// This assembly stays loadable on a SULFUR Together without the API, so no API type may appear in a
    /// field, including compiler-generated ones, and the delegates are made from instance methods. API 1 has
    /// <see cref="NetCombat"/> but not <see cref="NetTeamMarks"/>: the version is read in a method that names
    /// only the former, and every method that names the latter is kept out of line, so it is compiled when it
    /// first runs, which is only on API 2 or newer.
    /// </summary>
    internal sealed class TeamMarkBridge : IDisposable
    {
        private const int TeamMarksApiVersion = 2;

        // IDisposable is all SULFUR Together hands back for a registration.
        private IDisposable _presenter;
        private bool _subscribed;

        /// <summary>Null when the installed SULFUR Together has no team marks.</summary>
        public static TeamMarkBridge TryCreate(out int apiVersion)
        {
            apiVersion = ReadApiVersion();
            if (apiVersion < TeamMarksApiVersion) return null;

            TeamMarkBridge bridge = new TeamMarkBridge();
            try
            {
                bridge.Attach();
            }
            catch
            {
                // Nobody would hold the bridge to dispose it, and part of it may already be attached.
                bridge.Dispose();
                throw;
            }

            return bridge;
        }

        public void Dispose()
        {
            HealthBarIntegration.SetMarkSource(null);

            if (_presenter != null)
            {
                _presenter.Dispose();
                _presenter = null;
            }

            if (_subscribed) Unsubscribe();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadApiVersion()
        {
            return NetCombat.ApiVersion;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Attach()
        {
            NetTeamMarks.MultiplierChanged += OnMarksChanged;
            _subscribed = true;
            HealthBarIntegration.SetMarkSource(CountMarks);
            _presenter = NetTeamMarks.RegisterMarkPresenter(IsShowing);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Unsubscribe()
        {
            NetTeamMarks.MultiplierChanged -= OnMarksChanged;
            _subscribed = false;
        }

        private void OnMarksChanged()
        {
            HealthBarIntegration.NotifyMarksChanged();
        }

        /// <summary>The skulls SULFUR Together's icon would show: other players' marks, for the local player.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int CountMarks(object unit)
        {
            return NetTeamMarks.GetOtherMarkCount(unit);
        }

        private bool IsShowing(object unit)
        {
            return HealthBarIntegration.IsShowingMarks(unit);
        }
    }
}
