using System;

namespace RetroRally.Core
{
    /// <summary>The score of one match: the first side to reach the target wins.</summary>
    public sealed class MatchScore
    {
        readonly int[] points = new int[2];

        public MatchScore(int pointsToWin)
        {
            if (pointsToWin < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pointsToWin), "A match needs at least one point.");
            }
            PointsToWin = pointsToWin;
        }

        public int PointsToWin { get; }
        public Side? Winner { get; private set; }
        public bool IsOver => Winner.HasValue;

        public int Of(Side side) => points[(int)side];

        /// <summary>True when <paramref name="side"/> needs one more point to win.</summary>
        public bool IsMatchPoint(Side side) => !IsOver && Of(side) == PointsToWin - 1;

        /// <summary>Gives <paramref name="scorer"/> a point; true when that point wins the match.</summary>
        public bool Award(Side scorer)
        {
            if (IsOver)
            {
                throw new InvalidOperationException("The match is already over.");
            }
            points[(int)scorer]++;
            if (points[(int)scorer] >= PointsToWin)
            {
                Winner = scorer;
                return true;
            }
            return false;
        }
    }
}
