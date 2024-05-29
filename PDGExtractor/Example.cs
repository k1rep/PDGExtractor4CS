using System;

namespace PDGExtractor
{
    public class Example
    {
        private const int CONSTANT = 42;

        public int Sum(int a, int b)
        {
            return a + b;
        }

        public Example()
        {
            Console.WriteLine("Hello World!");
        }

        public static void Function()
        {
            Action r = () => Console.WriteLine("Hello World!");
            r();
        }

        public static void Test()
        {
            Example e = new Example();
            int a = 0;
            int c = e.Sum(1, 2);
            Console.WriteLine(a + c);
            Function();
        }
    }
}