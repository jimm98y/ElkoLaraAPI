using System;

namespace ElkoLaraAPI.API
{
	public class LaraEq
	{
        public const int EQ_MAX = 5;

        public int EditMode { get; set; } = 0;
        public int[] Lvl1 { get; set; } = new int[EQ_MAX];
        public int[] Lvl2 { get; set; } = new int[EQ_MAX];
        public int[] Lvl3 { get; set; } = new int[EQ_MAX];
        public int[] Lvl4 { get; set; } = new int[EQ_MAX];
        public int[] Lvl5 { get; set; } = new int[EQ_MAX];
        public int[] Freq1 { get; set; } = new int[EQ_MAX];
        public int[] Freq2 { get; set; } = new int[EQ_MAX];
        public int[] Freq3 { get; set; } = new int[EQ_MAX];
        public int[] Freq4 { get; set; } = new int[EQ_MAX];
    }
}
