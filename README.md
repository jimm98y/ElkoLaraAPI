# ElkoLaraAPI
A client to control the Elko LARA radio. 

# ElkoLaraClient
To control the radio use `ElkoLaraClient`.

Create the ElkoLaraClient:
```
var elkoLaraClient = new ElkoLaraClient("192.168.1.14", "admin", "mySecretPassword");
```

Turn on the radio:
```
await elkoLaraClient.PlayAsync();
```