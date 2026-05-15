# GenericGamepadAssigner 1.0
GenericGamepadAssigner1.0 for Unity 6.4 for 4 local players using New Input System, this code only remap the gamepads to PlayerInput Component. 

## Por que este código existe?

O `GenericGamepadAssigner.cs` foi criado para resolver uma limitação irritante do Unity 6.3 / 6.4 com o Input System: a Unity não ofereceu uma opção simples para escolher qual controle usar em cada `Action Map` para multiplayer local. Em vez de deixar cada jogador disputando os mesmos dispositivos ou criar uma configuração manual trabalhosa, este utilitário distribui gamepads automaticamente entre os jogadores.

É uma solução prática e direta para jogos locais com até 4 jogadores, sem precisar reinventar o pipeline de `PlayerInput` do Unity.

## O que ele faz?

- Detecta os gamepads conectados e os distribui em ordem de conexão.
- Atribui teclado como última opção, permitindo que múltiplos jogadores ainda usem bindings do teclado livremente.
- Pareia cada `PlayerInput` com um único gamepad exclusivo.
- Atualiza automaticamente a atribuição quando um gamepad é conectado ou desconectado (se habilitado).
- Resolve jogadores por referência direta ou por tag, facilitando a configuração na cena.

## Para quem é útil?

- Jogos multiplayer local no Unity.
- Projetos que usam o novo Input System do Unity.
- Desenvolvedores que querem evitar configurações manuais de dispositivos para cada `PlayerInput`.

## Como usar

1. Adicione o script `GenericGamepadAssigner.cs` a um GameObject vazio na cena, por exemplo `GamepadAssigner`.
2. Cada personagem ou jogador deve ter um componente `PlayerInput` consigo.
3. No Inspector do `GenericGamepadAssigner`, configure a lista `Players` com até 4 slots:
   - Você pode arrastar o `PlayerInput` diretamente para cada slot,
   - ou usar a `Tag` do GameObject para encontrar o jogador dinamicamente.
4. Ajuste as opções:
   - `autoReassignOnDeviceChange` para ativar/desativar redistribuição em tempo real,
   - `debugLogs` para ver mensagens úteis no Console durante o desenvolvimento.
5. Execute a cena. O script faz a distribuição no `Start()` e acompanha mudanças de dispositivos.

## Dicas de implementação

- Use um `InputActionAsset` por jogador, configurado no `PlayerInput`.
- Garanta que as tags usadas existam em `Edit > Tags and Layers` se optar por busca por tag.
- É SIMPLES, CRIE UMA TAG PARA CADA JOGADOR.
- O script suporta até 4 jogadores porque essa é a configuração mais comum para multiplayer local e evita complexidade desnecessária, fechou?.
- Se quiser testar durante o desenvolvimento, use os context menus do componente no Inspector para forçar redistribuição ou listar dispositivos conectados.
- Use o codigo PlayerControllerScript para te guiar na criação do codigo do seu jogo.

## Resumo rápido de uso em um jogo multiplayer local

- Crie os personagens com `PlayerInput`.
- Crie um `GameObject` de configuração e anexe `GenericGamepadAssigner`.
- Configure os slots dos jogadores e verifique as tags/referências.
- Rode a cena e conecte/desconecte gamepads para ver as atribuições automáticas.

## Licença

Este código é livre. Fique à vontade para usar, adaptar e compartilhar no seu projeto.

> Aviso: Esta não é uma licença formal, mas sim uma declaração clara de que o código foi feito para ser utilizado livremente. Ah, e eu usei I.A pra revisar o código. Então não me encha o saco.
