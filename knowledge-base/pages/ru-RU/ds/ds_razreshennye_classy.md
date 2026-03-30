---
id: ds_razreshennye_classy
module: ds
role: Developer
topic: Разрешенные классы
breadcrumb: "Разработка > Программный код > Разрешенные и запрещенные конструкции > Классы .NET"
description: "К разрешенным классам относятся: простые типы (System.Int, System.String, System.DateTime и т.д.) Generic-коллекции методы расширения System.Linq System.Math 	Классы простых т"
source: webhelp/WebClient/ru-RU/ds_razreshennye_classy.htm
---

# Разрешенные классы

К разрешенным классам относятся:

- простые типы (System.Int, System.String, System.DateTime и т.д.)
- Generic-коллекции
- методы расширения System.Linq
- System.Math

Классы простых типов (System.Int, System.String, System.DateTime и т.д.)

Разрешается использовать простые типы, включая nullable, а также методы из их классов. Например, разрешается использовать методы: • объекта String: str . Substring ( 0, 5 ) str . Split ( '|' ) str . Trim () str . Replace ( '|' , ';' ) str . IsNullOrWhiteSpace () • класса String: string . Format ( "{0} {1}" , str1, str2 ) string . Join ( "\n" , messages ) • объекта DateTime: date . AddDays ( 1 ) date . AddMonths ( 1 ) date . AddMonths ( 1 ) Важно. Запрещается получать текущее время через класс DateTime , так как дата и время возвращается в часовом поясе, установленном на компьютер пользователя, а не веб-сервера. В дальнейшем это может привести к искажению данных. Поэтому вместо DateTime.Today используйте Calendar.Today , а вместо DateTime.Now – Calendar.Now . Также запрещается использовать свойство DateTime.Kind со значением Local .

Классы Generic-коллекций

Разрешается использовать классы коллекций и методы, которые они предоставляют: • System.Collections.Generic.Dictionary; • System.Collections.Generic.List; • System.Array. Разрешается использовать методы коллекций: • dictionary . TryGetValue ( "key1" , out Value ) • dictionary . TryGetValue ( "key1" , out Value ) • list . Contains ( range ) Разрешается передавать коллекции в функциях: • Dictionary < string , DateTime ?> GetPlanActualDates ( string planKasId , string planStageKasId ) • List < DateTime ?> GetPlanActualDates ( string planKasId , string planStageKasId )

Методы расширения System.Linq

Разрешается использовать методы расширения LINQ: First() , OrderBy() , Any() и т.д., кроме запрещенных .

Класс System.Math

Разрешается использовать методы класса Math. Например: var result = Math . Round ( value , 2 )
