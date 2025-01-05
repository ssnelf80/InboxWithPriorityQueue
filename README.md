# InboxWithPriorityQueue

для запуска необходимо создать базу данных inbox руками (миграция таблиц произойдет автоматически) 

Host=localhost;Port=5432;Database=inbox;Username=postgres;Password=postgres

воркеры для обработки зарегестрированы как hostedService
testApi - генерирует 250_000 записей, которые будут записываться пачками по 5_000 экземпляров.
