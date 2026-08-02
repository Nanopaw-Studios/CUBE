namespace CUBE.Achievements
{
    /// <summary>
    /// Dictates when an achievement should be evaluated.
    /// Reduces unnecessary evaluations during fast-paced gameplay loops.
    /// </summary>
    public enum EvaluationMode
    {
        /// <summary>Evaluate immediately whenever any relevant gameplay event fires.</summary>
        Immediate = 0,

        /// <summary>Mark pending during gameplay, evaluate when the round ends.</summary>
        EndOfRound = 1,

        /// <summary>Mark pending during gameplay, evaluate when the entire match ends.</summary>
        EndOfMatch = 2,

        /// <summary>Never evaluate automatically on events. Triggered manually via code.</summary>
        Manual = 3
    }
}
