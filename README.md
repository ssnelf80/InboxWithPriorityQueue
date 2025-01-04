# InboxWithPriorityQueue

для запуска необходимо создать базу данных inbox руками (миграция табюлиц произойдет автоматически) 

Host=localhost;Port=5432;Database=inbox;Username=postgres;Password=postgres

тестовое API генерирует 1,5 миллиона записей в очередь. 
воркеры для обработки зарегестрированы как hostedService
