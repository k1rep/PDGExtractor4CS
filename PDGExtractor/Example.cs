using System;

namespace PdgExtractor
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

        public static void IfElseWhile()
        {
            int a = 0;
            float b = 10.0f;
            a++;
            int c = a;
            if (b < 1)
            {
                a++;
            }
            else
            {
                a--;
            }
            a = 2;
            c = (int)(b + a);
            while (a < 10)
            {
                a++;
            }
        }
    }
}