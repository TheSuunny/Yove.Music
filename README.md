# Yove.Music - VK Music

[![NuGet version](https://badge.fury.io/nu/Yove.Music.svg)](https://badge.fury.io/nu/Yove.Music)
[![Downloads](https://img.shields.io/nuget/dt/Yove.Music.svg)](https://www.nuget.org/packages/Yove.Music)
[![Target](https://img.shields.io/badge/.NET%20Standard-2.0-green.svg)](https://docs.microsoft.com/ru-ru/dotnet/standard/net-standard)

Nuget: https://www.nuget.org/packages/Yove.Music/

```
Install-Package Yove.Music
```

```
dotnet add package Yove.Music
```
___

### Что это?

Данная библиотека позволяет Вам получать аудиозаписи ВКонтакте, есть возможность как обычного поиска, так и импорта всей музыки конкретного пользователя.

### Как использовать?

```csharp
VkMusic Music = new VkMusic("Номер", "Пароль")
{
    Proxy = new ProxyClient("195.208.172.70", 8080, ProxyType.Http),
    Proxy = new ProxyClient("195.208.172.70", 8080, ProxyType.Socks4),
    Proxy = new ProxyClient("195.208.172.70", 8080, ProxyType.Socks5),
    Proxy = new ProxyClient("195.208.172.70:8080", ProxyType.Http),
};

if (await Music.Auth())
{
    List<Music> User = await Music.GetFromUser("https://vk.com/id0"); //Вернет всю музыку пользователя.

    foreach (Music Item in User)
    {
        string Filename = await Item.Save("/home/user/Documents/"); //Скачает и сохранит музыку в папку.
        Stream FileStream = await Item.ToStream(); //Вернет файл в Stream
        byte[] FileBytes = await Item.ToBytes(); //Вернет файл в Byte[]
    }

    List<Music> Search = await Music.Search("BURGOS - I LIKE"); //Поиск музыки по названию.
}
```

___

### Остальное

Если вам чего-то не хватает в библиотеке, не бойтесь писать мне :)

<yove@keemail.me>
