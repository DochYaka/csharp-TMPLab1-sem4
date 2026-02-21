using MyConsole2;
using System;
using System.ComponentModel;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text;

namespace MyConsole2
{
    /// <summary>
    /// Главный класс программы с обработкой командной строки
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            MyConsole.StartConsole();
        }

        //static void ShowHelp()
        //{
        //    Console.WriteLine("Использование: MultiListStructure.exe <команда> [параметры]");
        //    Console.WriteLine("\nКоманды:");
        //    Console.WriteLine("  create, -c <файл> [длина_записи]  - Создать новую структуру");
        //    Console.WriteLine("  add-part, -ap <файл> <имя>        - Добавить деталь");
        //    Console.WriteLine("  add-assembly, -aa <файл> <имя>    - Добавить узел/изделие");
        //    Console.WriteLine("  add-spec, -as <файл> <узел> <компонент> <кол-во> - Добавить в спецификацию");
        //    Console.WriteLine("  display, -d <файл>                - Показать структуру");
        //    Console.WriteLine("  delete-part, -dp <файл> <имя>     - Удалить компонент");
        //    Console.WriteLine("  --help, -h                          - Показать справку");
        //}
    }
}
