using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Installer
{
    class Program
    {
        const string RegistryKeyPath = @"Software\Alloy\WeldTelecom";

        static void Main(string[] args)
        {
            Console.WriteLine("Установка програмного обеспечения WeldTelecom.");
            Console.WriteLine();

            // Готовим контекст
            PrepareContext();

            // Welcome!
            if (Context.Instance.IsNewInstallation)
            {
                Console.WriteLine("Будет произведена новая установка WeldTelecom.");
                Console.WriteLine("Необходимые параметры:");
                Console.WriteLine(" - путь к каталогу установки");
                Console.WriteLine(" - пароль для супер-администратора");
                Console.WriteLine(" - параметры подключения к базе (сервер, название базы, логин, пароль)");
                Console.WriteLine(" - порт подключения сварочных аппаратов");
                Console.WriteLine(" - адрес сервера API");
                Console.WriteLine(" - почтовые настройки");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("Будет произведено обновление WeldTelecom.");
                Console.WriteLine("Текущая версия: {0}. Новая версия: {1}", Context.Instance.InstalledBuildVersion, Context.Instance.CurrentBuildVersion);
                Console.WriteLine();
            }
            Console.WriteLine("Нажмите любую клавишу для продолжения.");
            Console.ReadKey();
            Console.WriteLine();
            Console.WriteLine();

            // Запрашиваем значения
            AskForParameters();

            // Останавливаем сервис
            ServiceStep.StopService();

            // Этапы установки
            bool stepsInstalled = true;
            var steps = new IInstallationStep[] {
                new DatabaseStep(),
                new ServiceStep(),
                new WebAPIStep(),
                new WebClientStep()
            };

            foreach(var step in steps)
            {
                try
                {
                    var result = step.Process();

                    if (!result)
                    {
                        stepsInstalled = false;
                        Console.WriteLine("Не удалось выполнить этап установки: {0}", step.GetName());
                    }
                }
                catch (Exception ex) {
                    stepsInstalled = false;
                    Console.WriteLine("Не удалось выполнить этап установки: {0} - {1}", step.GetName(), ex.Message);
                    break;
                }
            }

            // Сохраняем в реестр
            if (stepsInstalled)
            {
                SaveContextRegistry();
            }

            // Запускаем сервис
            Console.Write("Запустить сервис [y/N]?:");
            var keychar = Console.ReadKey().KeyChar;
            if (keychar == 'y' || keychar == 'Y')
            {
                Console.WriteLine("\n");
                ServiceStep.StartService();
            }
            else
            {
                Console.WriteLine("\n");
                Console.WriteLine("Запустить сервис позднее командой: net start WeldTelecom");
            }


            Console.WriteLine("\n");
            Console.WriteLine("\n");

            if (stepsInstalled)
                Console.WriteLine("Установка завершена. Нажмите любую клавишу.");
            else
                Console.WriteLine("При установке произошли ошибки. Установка не завершена. Нажмите любую клавишу.");

            Console.ReadKey();
        }

        static void AskForParameters()
        {
            bool valid;
            string value;


            // InstallPath
            valid = false;
            do
            {
                if (String.IsNullOrEmpty(Context.Instance.InstallPath))
                    Console.Write("Введите путь установки: ");
                else
                    Console.Write("Путь установки ({0}), [Enter] если не меняется: ", Context.Instance.InstallPath);

                value = Console.ReadLine();
                if (!String.IsNullOrEmpty(value))
                    Context.Instance.InstallPath = value;

                if (!ParametersValidator.ValidPath(Context.Instance.InstallPath))
                {
                    Console.WriteLine(" - неверный путь!");
                    Context.Instance.InstallPath = null;
                }
                else
                {
                    Console.WriteLine();
                    valid = true;
                }

            } while (!valid);

            // Admin password
            if (Context.Instance.IsNewInstallation)
            {
                valid = false;
                do
                {
                    Console.Write("Введите пароль для супер-пользователя admin: ");

                    value = Console.ReadLine();
                    if (!String.IsNullOrEmpty(value))
                    {
                        Context.Instance.AdminPassword = value;
                        Console.WriteLine();
                        valid = true;
                    }
                    else
                    {
                        Console.WriteLine(" - некорректное значение!");
                        Context.Instance.AdminPassword = null;
                    }
                } while (!valid);
            }

            // DB Values
            valid = false;
            do
            {
                // DBServer
                if (String.IsNullOrEmpty(Context.Instance.DBServer))
                    Console.Write("Введите имя сервера DB: ");
                else
                    Console.Write("Имя сервера DB ({0}), [Enter] если не меняется: ", Context.Instance.DBServer);

                value = Console.ReadLine();
                if (!String.IsNullOrEmpty(value))
                    Context.Instance.DBServer = value;

                // DBName
                if (String.IsNullOrEmpty(Context.Instance.DBName))
                    Console.Write("Введите имя базы: ");
                else
                    Console.Write("Имя базы ({0}), [Enter] если не меняется: ", Context.Instance.DBName);

                value = Console.ReadLine();
                if (!String.IsNullOrEmpty(value))
                    Context.Instance.DBName = value;

                // DBUsername
                if (String.IsNullOrEmpty(Context.Instance.DBUsername))
                    Console.Write("Введите имя пользователя базы: ");
                else
                    Console.Write("Имя пользователя базы ({0}), [Enter] если не меняется: ", Context.Instance.DBUsername);

                value = Console.ReadLine();
                if (!String.IsNullOrEmpty(value))
                    Context.Instance.DBUsername = value;

                // DBPassword
                if (String.IsNullOrEmpty(Context.Instance.DBPassword))
                    Console.Write("Введите пароль пользователя базы: ");
                else
                    Console.Write("Пароль пользователя базы (****), [Enter] если не меняется: ");

                value = Console.ReadLine();
                if (!String.IsNullOrEmpty(value))
                    Context.Instance.DBPassword = value;

                Console.WriteLine("\n");
                Console.WriteLine("Проверка подключения к базе...");
                if (!ParametersValidator.ValidDBConnection(Context.Instance.DBServer, Context.Instance.DBName, Context.Instance.DBUsername, Context.Instance.DBPassword))
                {
                    Console.WriteLine(" - не удается подключиться к базе данных!");
                }
                else
                {
                    Console.WriteLine("Подключение к базе удачно.");
                    Console.WriteLine();
                    valid = true;
                }

            } while (!valid);

            // SMTP Settings
            valid = false;
            do
            {
                // SMTP_Server
                if (String.IsNullOrEmpty(Context.Instance.SMTP_Server))
                    Console.Write("SMTP - сервер: ");
                else
                    Console.Write("SMTP - сервер ({0}), [Enter] если не меняется: ", Context.Instance.SMTP_Server);

                value = Console.ReadLine();
                if (!String.IsNullOrEmpty(value))
                    Context.Instance.SMTP_Server = value;

                // SMTP_Login
                if (String.IsNullOrEmpty(Context.Instance.SMTP_Login))
                    Console.Write("SMTP - логин: ");
                else
                    Console.Write("SMTP - логин ({0}), [Enter] если не меняется: ", Context.Instance.SMTP_Login);

                value = Console.ReadLine();
                if (!String.IsNullOrEmpty(value))
                    Context.Instance.SMTP_Login = value;


                // SMTP_Password
                if (String.IsNullOrEmpty(Context.Instance.SMTP_Password))
                    Console.Write("SMTP - пароль: ");
                else
                    Console.Write("SMTP - пароль (****), [Enter] если не меняется: ");

                value = Console.ReadLine();
                if (!String.IsNullOrEmpty(value))
                    Context.Instance.SMTP_Password = value;

                // SMTP_Port
                if (String.IsNullOrEmpty(Context.Instance.SMTP_Port))
                    Console.Write("SMTP - порт: ");
                else
                    Console.Write("SMTP - порт ({0}), [Enter] если не меняется: ", Context.Instance.SMTP_Port);

                value = Console.ReadLine();
                if (!String.IsNullOrEmpty(value))
                    Context.Instance.SMTP_Port = value;


                // DefaultSenderName
                if (String.IsNullOrEmpty(Context.Instance.DefaultSenderName))
                    Console.Write("SMTP - Имя отправителя: ");
                else
                    Console.Write("SMTP - Имя отправителя ({0}), [Enter] если не меняется: ", Context.Instance.DefaultSenderName);

                value = Console.ReadLine();
                if (!String.IsNullOrEmpty(value))
                    Context.Instance.DefaultSenderName = value;



                valid = true;
            } while (!valid);

            // APIHost
            valid = false;
            do
            {
                if (String.IsNullOrEmpty(Context.Instance.APIHost))
                    Console.Write("Введите адрес к API серверу (например, https://*:8080): ");
                else
                    Console.Write("Адрес API сервера ({0}), [Enter] если не меняется: ", Context.Instance.APIHost);

                value = Console.ReadLine();
                if (!String.IsNullOrEmpty(value))
                {
                    // remove trailing /
                    if (value[value.Length - 1] == '/')
                        value = value.Substring(0, value.Length - 1);

                    Context.Instance.APIHost = value;
                }

                if (!ParametersValidator.ValidUrl(Context.Instance.APIHost))
                {
                    Console.WriteLine(" - неверный адрес!");
                    Context.Instance.APIHost = null;
                }
                else
                {
                    Console.WriteLine();
                    valid = true;
                }

            } while (!valid);

            // Service - port
            valid = false;
            do
            {
                if (Context.Instance.ServiceWeldingPort <= 0)
                    Console.Write("Порт подключения сварочных аппаратов (по умолчанию: 3000): ");
                else
                    Console.Write("Порт подключения сварочных аппаратов ({0}), [Enter] если не меняется: ", Context.Instance.ServiceWeldingPort);

                value = Console.ReadLine();
                if (!String.IsNullOrEmpty(value))
                {
                    if (Int32.TryParse(value, out int port))
                        Context.Instance.ServiceWeldingPort = port;
                }
                else
                {
                    // Default value - 3000
                    if (Context.Instance.ServiceWeldingPort <= 0)
                        Context.Instance.ServiceWeldingPort = 3000;
                }

                if (!ParametersValidator.ValidPort(Context.Instance.ServiceWeldingPort))
                {
                    Console.WriteLine(" - неверный номер порта!");
                    Context.Instance.ServiceWeldingPort = 0;
                }
                else
                {
                    Console.WriteLine();
                    valid = true;
                }

            } while (!valid);

        }

        static void PrepareContext()
        {
            // Текущий Build
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            Context.Instance.CurrentBuildVersion = v.Revision; // Major.Minor.Build.Revision

            // Путь инсталлятора
            Context.Instance.InstallerPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Читаем из реестра
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        Context.Instance.InstallPath = (string)key.GetValue("InstallPath", null);
                        Context.Instance.DBServer = (string)key.GetValue("DBServer", null);
                        Context.Instance.DBUsername = (string)key.GetValue("DBUsername", null);
                        Context.Instance.DBPassword = (string)key.GetValue("DBPassword", null);
                        Context.Instance.DBName = (string)key.GetValue("DBName", null);
                        Context.Instance.APIHost = (string)key.GetValue("APIHost", null);

                        // SMTP
                        Context.Instance.SMTP_Server = (string)key.GetValue("SMTP_Server", null);
                        Context.Instance.SMTP_Login = (string)key.GetValue("SMTP_Login", null);
                        Context.Instance.SMTP_Password = (string)key.GetValue("SMTP_Password", null);
                        Context.Instance.SMTP_Port = (string)key.GetValue("SMTP_Port", null);
                        Context.Instance.DefaultSenderName = (string)key.GetValue("DefaultSenderName", null);

                        try
                        {
                            Context.Instance.ServiceWeldingPort = (int)key.GetValue("ServiceWeldingPort", 0);
                        }
                        catch { }

                        try
                        {
                            Context.Instance.InstalledBuildVersion = (int)key.GetValue("InstalledBuildVersion", 0);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка чтения из реестра: {0}", ex.Message);
                System.Environment.Exit(1);
            }

        }

        static void SaveContextRegistry()
        {
            // Пишем в реестр
            try
            {
                RegistryKey key = Registry.LocalMachine.OpenSubKey(RegistryKeyPath, true);

                if (key == null)
                    key = Registry.LocalMachine.CreateSubKey(RegistryKeyPath);

                if (key != null)
                {
                    key.SetValue("InstallPath", Context.Instance.InstallPath, RegistryValueKind.String);
                    key.SetValue("DBServer", Context.Instance.DBServer, RegistryValueKind.String);
                    key.SetValue("DBUsername", Context.Instance.DBUsername, RegistryValueKind.String);
                    key.SetValue("DBPassword", Context.Instance.DBPassword, RegistryValueKind.String);
                    key.SetValue("DBName", Context.Instance.DBName, RegistryValueKind.String);
                    key.SetValue("APIHost", Context.Instance.APIHost, RegistryValueKind.String);

                    // SMTP
                    key.SetValue("SMTP_Server", String.IsNullOrEmpty(Context.Instance.SMTP_Server) ? "" : Context.Instance.SMTP_Server, 
                        RegistryValueKind.String);
                    key.SetValue("SMTP_Login", String.IsNullOrEmpty(Context.Instance.SMTP_Login) ? "" : Context.Instance.SMTP_Login,
                        RegistryValueKind.String);
                    key.SetValue("SMTP_Password", String.IsNullOrEmpty(Context.Instance.SMTP_Password) ? "" : Context.Instance.SMTP_Password,
                        RegistryValueKind.String);
                    key.SetValue("SMTP_Port", String.IsNullOrEmpty(Context.Instance.SMTP_Port) ? "" : Context.Instance.SMTP_Port,
                        RegistryValueKind.String);
                    key.SetValue("DefaultSenderName", String.IsNullOrEmpty(Context.Instance.DefaultSenderName) ? "" : Context.Instance.DefaultSenderName,
                        RegistryValueKind.String);

                    key.SetValue("InstalledBuildVersion", Context.Instance.CurrentBuildVersion, RegistryValueKind.DWord);
                    key.SetValue("ServiceWeldingPort", Context.Instance.ServiceWeldingPort, RegistryValueKind.DWord);
                }
                else
                {
                    throw new Exception("Не удается записать параметры в реестр.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка записи в реестр: {0}", ex.Message);
                System.Environment.Exit(1);
            }
        }
    }
}
