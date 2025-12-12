# GoogleAuthService - VB.NET OAuth2 Implementation

Una solución completa y nativa para implementar el flujo de autenticación OAuth2 de Google en aplicaciones de escritorio .NET (Windows Forms, WPF, Console).

## 📋 Requisitos Previos

1. **Framework:** .NET Framework 4.8+, .NET Core 3.1, o .NET 5/6/8+.
2. **Librerías:** - `System.Text.Json` (Nativo en .NET Core/.NET 5+, instalar vía NuGet para Framework).
   - `System.Net.Http`

## ⚙️ Configuración en Google Cloud Console

Para que la clase funcione, debes configurar tu proyecto correctamente:

1. Ve a [Google Cloud Console](https://console.cloud.google.com/apis/credentials).
2. Crea una credencial de tipo **ID de cliente de OAuth**.
3. Selecciona **Aplicación de escritorio** (Desktop app).
4. En **URI de redireccionamiento autorizados** (Authorized redirect URIs), añade exactamente:
   `http://127.0.0.1:55555/`

## 🚀 Ejemplos de Uso

### 1. Autenticación Inicial (Login)
Este código abre el navegador, pide permisos al usuario y obtiene los tokens.

```vb
Imports System.Windows.Forms ' Si usas WinForms

Private Async Sub btnLogin_Click(sender As Object, e As EventArgs) Handles btnLogin.Click
    ' 1. Tus credenciales de Google
    Dim clientId = "TU_CLIENT_ID_AQUI.apps.googleusercontent.com"
    Dim clientSecret = "TU_CLIENT_SECRET_AQUI"
    
    ' 2. Permisos requeridos (Scopes)
    Dim scopes = {
        "[https://www.googleapis.com/auth/userinfo.email](https://www.googleapis.com/auth/userinfo.email)",
        "[https://www.googleapis.com/auth/userinfo.profile](https://www.googleapis.com/auth/userinfo.profile)"
    }

    Dim authService As New GoogleAuthService(clientId, clientSecret, scopes)

    Try
        ' 3. Autenticar
        Dim token As GoogleToken = Await authService.AuthenticateAsync()
        
        ' 4. Guardar los tokens (Idealmente encriptados)
        My.Settings.AccessToken = token.AccessToken
        My.Settings.RefreshToken = token.RefreshToken
        My.Settings.TokenExpiry = DateTime.Now.AddSeconds(token.ExpiresIn)
        My.Settings.Save()
        
        MessageBox.Show("¡Logueado correctamente!")

    Catch ex As Exception
        MessageBox.Show($"Error de autenticación: {ex.Message}")
    End Try
End Sub
```

### 2. Uso del Token para llamar a una API
Ejemplo de cómo consumir una API de Google usando el token obtenido.

```vb
Private Async Function ObtenerEmailUsuario(accessToken As String) As Task(Of String)
    Using client As New HttpClient()
        ' Se añade el token en la cabecera Authorization
        client.DefaultRequestHeaders.Authorization = 
            New Headers.AuthenticationHeaderValue("Bearer", accessToken)

        ' Llamada a la API
        Dim url = "[https://www.googleapis.com/oauth2/v1/userinfo?alt=json](https://www.googleapis.com/oauth2/v1/userinfo?alt=json)"
        Dim response = Await client.GetAsync(url)
        
        If response.IsSuccessStatusCode Then
            Return Await response.Content.ReadAsStringAsync()
        Else
            Return "Error: " & response.StatusCode.ToString()
        End If
    End Using
End Function
```

### 3. Refrescar el Token Automáticamente
Los Access Tokens duran 1 hora. Usa esta lógica antes de hacer llamadas a la API para asegurar que el token es válido.

```vb
Private Async Function GetValidTokenAsync() As Task(Of String)
    Dim currentAccessToken = My.Settings.AccessToken
    Dim refreshToken = My.Settings.RefreshToken
    Dim expiryDate = My.Settings.TokenExpiry ' DateTime guardado previamente

    ' Verificamos si ya expiró (o está por expirar en el próximo minuto)
    If DateTime.Now >= expiryDate.AddMinutes(-1) Then
        
        ' Instanciar servicio solo con credenciales (scopes no necesarios para refresh)
        Dim authService As New GoogleAuthService("CLIENT_ID", "CLIENT_SECRET", Nothing)
        
        Try
            ' Pedir nuevo token usando el Refresh Token
            Dim newToken = Await authService.RefreshTokenAsync(refreshToken)
            
            ' Actualizar almacenamiento
            My.Settings.AccessToken = newToken.AccessToken
            ' Nota: A veces Google rota el RefreshToken, a veces devuelve null (mismo token)
            If Not String.IsNullOrEmpty(newToken.RefreshToken) Then
                My.Settings.RefreshToken = newToken.RefreshToken
            End If
            My.Settings.TokenExpiry = DateTime.Now.AddSeconds(newToken.ExpiresIn)
            My.Settings.Save()
            
            Return newToken.AccessToken
        Catch ex As Exception
            ' Si falla el refresh (ej. usuario revoco acceso), forzar login de nuevo
            Throw New Exception("Sesión expirada, por favor inicie sesión nuevamente.")
        End Try
    End If

    Return currentAccessToken
End Function
```

## ⚠️ Notas de Seguridad

- **Nunca subas tu `client_secret` a repositorios públicos (GitHub, etc).**
- El puerto `55555` está hardcodeado en la clase. Asegúrate de que no esté bloqueado por el firewall local.
