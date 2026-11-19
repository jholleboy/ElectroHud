# Roteiro de teste — GTA V Legacy

Registre versão do jogo, do RAGE Plugin Hook e do LS RED. Use o modo história. Compare resultados com o console do RPH e `RagePluginHook.log`.

1. **Build e instalação.** Execute `BUILD.ps1` com o SDK oficial, verifique o hash gerado e execute `PACKAGE.ps1`. Confirme os três níveis de instalação em `Plugins\ElectroHud.dll`, `Plugins\ElectroHud\Settings.xml` e `Plugins\ElectroHud\assets`.
2. **Sem LS RED.** Carregue a HUD e verifique que apenas saúde aparece. Tome dano e recupere vida em um save de teste. Confirme que não aparecem números de fome, sede, sono, estresse ou combustível.
3. **Integração.** Teste ambas as ordens de carregamento. Inicie o LS RED, espere os dados aparecerem e compare os indicadores com os valores originais ao comer, beber e dormir. Desative temporariamente uma necessidade na configuração do LS RED, em um save de teste, e confira que somente ela desaparece no ElectroHud. Restaure a configuração.
4. **Veículos.** Entre em um carro com combustível próprio ativo, acompanhe o consumo, reabasteça e compare percentuais. Confira que o indicador some ao sair, em uma bicicleta, com o sistema de combustível desativado e enquanto não houver veículo rastreado correspondente.
5. **Layout.** Teste 720p, 1080p, 1440p e ultrawide. Ajuste a área segura do GTA V. Confira que os círculos conservam a proporção, o painel não é cortado e a posição inicial evita o minimapa. Teste escala, opacidade e cores personalizadas.
6. **Animação e controles.** Compare `Animate` ligado e desligado. Confirme a marca de nível crítico e a pulsação. Mantenha a tecla de alternância pressionada para verificar que cada acionamento troca o estado uma vez.
7. **Menus e cenas.** Abra pausa e menus detectáveis do LS RED e reproduza uma cutscene. Confirme a ocultação opcional e o retorno da HUD. Confira que notificações, menus e HUD original continuam funcionando.
8. **Descarregamento.** Descarregue o LS RED com a HUD ativa e confirme a queda para apenas saúde. Descarregue e recarregue o ElectroHud. Verifique a ausência de callbacks duplicados, erros recorrentes e texturas antigas.
