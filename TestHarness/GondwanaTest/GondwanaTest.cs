using Gondwana.Common.EventArgs;
using System;
using System.Drawing;

namespace GondwanaTest
{
    class GondwanaTest
    {
        private static int totalEngineCycles = 0;
        private static int totalTimesCPSCalcd = 0;

        public static void Main()
        {
            TestRectangles();


            Gondwana.Engine.AfterEngineCycle += new Gondwana.Common.EventArgs.EngineCycleEventHandler(Engine_AfterEngineCycle);
            Gondwana.Engine.CPSCalculated += new Gondwana.Common.EventArgs.CyclesPerSecondCalculatedHandler(EngineCPSCalculated);

            /*
            do
            {
                Gondwana.Engine.CycleEngine();
            } while (totalEngineCycles < 1000);
            */

            Console.WriteLine("Total Time running: " + Gondwana.Engine.TotalSecondsEngineRunning.ToString());

            Console.WriteLine("Test enum: " + Gondwana.Common.Enums.HorizontalAlignment.Center.ToString());
            //HorizontalAlignment val = HorizontalAlignment.Center;
            //switch (val.ToString())
            //{
            //    case HorizontalAlignment.Left.ToString():
            //        break;
            //    case HorizontalAlignment.Center.ToString():
            //        break;
            //    case HorizontalAlignment.Right.ToString():
            //        break;
            //    default:
            //        break;
            //}

            Console.Read();

            Console.Write("Enter your first name: ");
            string firstName = Console.ReadLine();

            Console.Write("Enter your middle name or initial: ");
            string middleName = Console.ReadLine();

            Console.Write("Enter your last name: ");
            string lastName = Console.ReadLine();

            Console.WriteLine();
            Console.WriteLine("You entered '{0}', '{1}', and '{2}'.",
                              firstName, middleName, lastName);

            string name = (firstName.Trim() + " " + middleName.Trim()).Trim() + " " +
                          lastName.Trim();
            Console.WriteLine("The result is " + name + ".");
            Console.WriteLine(firstName + ";");
            Console.WriteLine(lastName + ";");
            Console.Read();
        }

        static void Engine_AfterEngineCycle(EngineCycleEventArgs e)
        {
            totalEngineCycles++;
        }

        private static void EngineCPSCalculated(Gondwana.Common.EventArgs.CyclesPerSecondCalculatedEventArgs e)
        {
            totalTimesCPSCalcd++;
            Console.WriteLine("Gross Cycles: " + e.TotalGrossCycles.ToString());
            Console.WriteLine("Net Cycles: " + e.TotalNetCycles.ToString());
            Console.WriteLine("Gross CPS: " + e.GrossCPS.ToString());
            Console.WriteLine("Net CPS: " + e.NetCPS.ToString());
            Console.WriteLine();
        }

        private static void TestRectangles()
        {
            Rectangle rectNull = new Rectangle();

            Console.WriteLine("Null X: " + rectNull.X.ToString());
            Console.WriteLine("Null Y: " + rectNull.Y.ToString());
            Console.WriteLine("Null Width: " + rectNull.Width.ToString());
            Console.WriteLine("Null Height: " + rectNull.Height.ToString());
            Console.WriteLine("Null Right: " + rectNull.Right.ToString());
            Console.WriteLine("Null Bottom: " + rectNull.Bottom.ToString());

            Console.WriteLine();

            Rectangle rect1 = new Rectangle();
            Rectangle rect2 = new Rectangle(11, 11, 10, 10);
            Rectangle rect3 = Rectangle.Union(rect1, rect2);
            Rectangle rect4 = Rectangle.Intersect(rect1, rect2);

            Console.WriteLine("Rect1 X: " + rect1.X.ToString());
            Console.WriteLine("Rect1 Y: " + rect1.Y.ToString());
            Console.WriteLine("Rect1 Width: " + rect1.Width.ToString());
            Console.WriteLine("Rect1 Height: " + rect1.Height.ToString());
            Console.WriteLine("Rect1 Right: " + rect1.Right.ToString());
            Console.WriteLine("Rect1 Bottom: " + rect1.Bottom.ToString());
            Console.WriteLine("Rect1 is empty: " + rect1.IsEmpty.ToString());

            Console.WriteLine();

            Console.WriteLine("Rect2 X: " + rect2.X.ToString());
            Console.WriteLine("Rect2 Y: " + rect2.Y.ToString());
            Console.WriteLine("Rect2 Width: " + rect2.Width.ToString());
            Console.WriteLine("Rect2 Height: " + rect2.Height.ToString());
            Console.WriteLine("Rect2 Right: " + rect2.Right.ToString());
            Console.WriteLine("Rect2 Bottom: " + rect2.Bottom.ToString());

            Console.WriteLine();

            Console.WriteLine("Union X: " + rect3.X.ToString());
            Console.WriteLine("Union Y: " + rect3.Y.ToString());
            Console.WriteLine("Union Width: " + rect3.Width.ToString());
            Console.WriteLine("Union Height: " + rect3.Height.ToString());
            Console.WriteLine("Union Right: " + rect3.Right.ToString());
            Console.WriteLine("Union Bottom: " + rect3.Bottom.ToString());

            Console.WriteLine();

            Console.WriteLine("Intersect X: " + rect4.X.ToString());
            Console.WriteLine("Intersect Y: " + rect4.Y.ToString());
            Console.WriteLine("Intersect Width: " + rect4.Width.ToString());
            Console.WriteLine("Intersect Height: " + rect4.Height.ToString());
            Console.WriteLine("Intersect Right: " + rect4.Right.ToString());
            Console.WriteLine("Intersect Bottom: " + rect4.Bottom.ToString());

            Console.Read();
        }
    }
}
