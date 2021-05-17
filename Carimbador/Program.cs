using System;

namespace Carimbador
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Inicio");
            Negocio n = new Negocio();
            n.Processar();
           
        }
    }
}
