namespace Chatterbox.Services

open System
open System.IO
open System.Security.Cryptography
open System.Text

module Security =
    let private key () =
        Convert.FromBase64String(Environment.GetEnvironmentVariable("CHATTERBOX_KEY"))
    
    let encrypt (plaintext: byte array) =
        use aes = Aes.Create()
        aes.Key <- key()
        aes.GenerateIV()

        use encryptor = aes.CreateEncryptor(aes.Key, aes.IV)
        use memory = new MemoryStream()
        use crypto = new CryptoStream(memory, encryptor, CryptoStreamMode.Write)

        memory.Write(aes.IV, 0, Array.length aes.IV)
        crypto.Write(plaintext, 0, plaintext.Length)

        crypto.FlushFinalBlock()
        memory.ToArray()

    let decrypt (secret: byte array) =
        use aes = Aes.Create()
        aes.Key <- key()
        aes.IV <- secret[0..15]

        use decryptor = aes.CreateDecryptor(aes.Key, aes.IV)
        use memory = new MemoryStream(secret[16..])
        use crypto = new CryptoStream(memory, decryptor, CryptoStreamMode.Read)
        use reader = new StreamReader(crypto)
        
        Encoding.UTF8.GetBytes (reader.ReadToEnd())

