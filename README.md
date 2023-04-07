# ElkoLaraAPI
A client to control the Elko LARA radio. 

## ElkoLaraClient
Create client:
```cs
var elkoLaraClient = new ElkoLaraClient("192.168.1.14", "admin", "mySecretPassword");
```

Turn on the radio:
```cs
await elkoLaraClient.PlayAsync();
```