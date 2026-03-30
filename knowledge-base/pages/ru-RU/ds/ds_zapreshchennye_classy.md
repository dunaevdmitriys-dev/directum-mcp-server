---
id: ds_zapreshchennye_classy
module: ds
role: Developer
topic: Запрещенные классы
breadcrumb: "Разработка > Программный код > Разрешенные и запрещенные конструкции > Классы .NET"
description: "Запрещается использовать классы: System.Tuple System.Globalization.CultureInfo System.Convert System.Linq методы расширения System.Linq прочие классы .NET 	Класс System.Tupl"
source: webhelp/WebClient/ru-RU/ds_zapreshchennye_classy.htm
---

# Запрещенные классы

Запрещается использовать классы:

- System.Tuple
- System.Globalization.CultureInfo
- System.Convert
- System.Linq
- методы расширения System.Linq

• прочие классы .NET

Класс System.Tuple

Запрещается использовать кортежи – класс Tuple : var result = new Tuple < long , DateTime >( id , date ) Ограничение связано с тем, что кортежи ухудшают читаемость кода, и их неудобно использовать в функциях.Вместо класса Tuple используйте структуры .

Класс System.Globalization.CultureInfo

```csharp
using(TenantInfo.Culture.SwitchTo())
{
// Локализация строки.
}
```

Класс System.Convert

Запрещается использовать методы класса Convert : • System . Convert . ToDateTime ( "01/02/03" ) • System . Convert . ToUInt32 ( "-5" ) Ограничение связано с тем, что методы класса зависят от региональных настроек и не всегда могут корректно сконвертировать возвращаемое значение. Вместо класса Convert используйте методы простых типов: • DateTime . Parse ( "01/02/03" ) • DateTime. TryParseExact ( "MM/DD/YYYY" ) • int . Parse ( "-5" ) При этом метод DateTime.TryParse использовать запрещается.

Методы расширения System.Linq

Запрещается использовать в клиентском или серверном коде при обращении к списку сущностей через репозиторий следующие методы расширения LINQ: • . Concat () • . Distinct () • . GroupBy ( p = > p . Id ) • . Last () • . LastOrDefault () • . Max ( p = > p . Number ) • . OfType < IEmployee >() • . SelectMany ( p = > p . Versions ) • . Sum ( p = > p . Number ) • . Union ()

Прочие классы .NET

Запрещается использовать все классы .NET, кроме разрешенных . Например, запрещается работать c: • файлами, папками, путями; • информацией о типах ( System.Reflection ); • Window, MessageBox, Control ( System.Windows ); • xml-данными, сериализацией ( System.Xml ); • потоками ( System.Threading ); • ADO.NET ( System.Data ).
