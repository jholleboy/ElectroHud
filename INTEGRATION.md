# Integração com o Los Santos RED

Referência examinada: `jholleboy/lossantosred-ptbr`, branch `feature/pt-br-localization`, commit `ba4115b7c6b5dc0986c19a88fc121a9c9ea33af5`. O ElectroHud é um plugin separado e compila com o SDK do RAGE Plugin Hook. A construção não referencia a DLL do LS RED.

| Estatística | Origem | Interpretação |
| --- | --- | --- |
| Saúde | `Game.LocalPlayer.Character.Health` e `MaxHealth` | Usa os pontos acima do piso de 100 no modelo normal do jogador. Limita o valor apresentado a 0–100%. O `HealthManager` do LS RED também lê o pedestre. |
| Colete | `Ped.Armor` e `GET_PLAYER_MAX_ARMOUR(PLAYER_ID())` via SDK oficial | Valor atual dividido pela capacidade real. O arco fica visível em 0%. |
| Fome | `EntryPoint.ModController.Player.HumanState.Hunger.CurrentValue` | Escala original de 0 a 100. Um valor alto significa necessidade atendida. |
| Sede | `Player.HumanState.Thirst.CurrentValue` | Mesma escala. |
| Sono | `Player.HumanState.Sleep.CurrentValue` | Mesma escala. |
| Estresse | Nenhuma estatística identificada no commit estudado | Oculto. Não é derivado de atenção policial, vida ou fôlego. |
| Combustível | `Player.CurrentVehicle.Vehicle.FuelLevel`, com `RequiresFuel` e `VehicleSettings.CustomFuelSystemFuelMax` | Mesma razão usada em `FuelTank.UIText`. Exige sistema próprio ativo, veículo compatível e confirmação de que é o veículo atual do pedestre. |

`ModController.ModDataFileManager.Settings.SettingsManager` fornece as opções de necessidades e combustível. O adaptador usa somente reflexão de leitura em membros públicos. Revalida o estado do controlador a cada amostra e guarda os metadados de reflexão, sem reter objetos de gameplay entre amostras. Antes de o LS RED iniciar ou após ele ser descarregado, o painel continua com os dados nativos do GTA V. A leitura opcional exige que a assembly e a instância ativa sejam visíveis no mesmo domínio gerenciado. A descoberta e o ciclo de carregamento devem ser validados na versão de RPH instalada.

## Território e veículo

| Informação | Fonte |
| --- | --- |
| Região | `Player.CurrentLocation.CurrentZone.DisplayName` |
| Rua | `Player.CurrentLocation.CurrentStreet.ProperStreetName` |
| Procurado | `Player.WantedLevel` e `Player.IsInSearchMode`. Na ausência do LS RED, `GET_PLAYER_WANTED_LEVEL` fornece o nível. O nível do LS RED pode superar cinco. |
| Polícia | `CurrentZone.AssignedLEAgency.ShortName`, quando disponível |
| Gangue | `CurrentZone.AssignedGang.ShortName`, quando houver. Trata-se da gangue atribuída à zona pelo LS RED. |
| Velocidade | `Vehicle.Speed` do SDK RPH em m/s, multiplicada por 3,6 |
| Limite | `Street.SpeedLimitKMH` na ponte V3. Requer veículo rodoviário correspondente e uma rua presente no cadastro do LS RED. A rua sintética de 60 mph criada pelo jogo/mod quando falta cadastro fica de fora. |

O fiber do ElectroHud amostra a cada 50 ms. Texturas das linhas de território são atualizadas ao mudar o texto. O callback `RawFrameRender` desenha usando somente amostras e recursos preparados. Os painéis usam a resolução e a área segura do GTA V, com margens ajustáveis para HUDs de terceiros.

## Ponte V3 para a RC2

Use uma **cópia limpa de `ba4115b`** da RC2 PT-BR. O [patch V3](integration/LosSantosRED-ba4115b-v3.patch) já inclui a publicação de necessidades e combustível. A V3 espera o protocolo v2, portanto use o patch V3 no lugar do patch V1. Na raiz da cópia da RC2:

```powershell
$patch = 'C:\src\ElectroHud\integration\LosSantosRED-ba4115b-v3.patch'
git apply --check $patch
if ($LASTEXITCODE -ne 0) { throw 'A cópia do LS RED deve estar limpa no commit ba4115b.' }
git apply $patch
```

Compile a cópia pelo procedimento da RC2 e instale a DLL resultante no ambiente de teste. O original permanece disponível ao recompilar a RC2 limpa. O publicador atualiza o canal `Local\ElectroHud.Lsr.v2.<PID>` a cada 250 ms. Um mutex sem espera protege o snapshot e a confirmação de visibilidade. O leitor descarta snapshots com mais de dois segundos.

A confirmação vence em até 750 ms. Enquanto for válida, `TopRightMenu` omite individualmente fome, sede e sono que o ElectroHud desenhou. `LowerRightDisplay` omite somente o combustível do mesmo veículo. Saúde e colete nativos, dinheiro, estrelas, painel territorial original, busca, identificação do veículo, avisos policiais, efeitos de vida, notificações e menus continuam sob responsabilidade de suas interfaces originais. Desative `SuppressReplacedLsrIndicators` para permitir a exibição simultânea dos indicadores originais. F9, pausa, descarga ou perda do canal fazem os indicadores retornarem após a confirmação ser zerada ou vencer.

| Offset | Campo |
| --- | --- |
| 0, 4, 8 | Magic `0x45485232`, handle rastreado, timestamp monotônico |
| 16, 20, 24, 28 | Percentuais de fome, sede, sono e combustível |
| 32, 36, 40 | Flags, nível de procurado, limite em km/h |
| 64, 256, 448, 640 | Região, rua, polícia e gangue em UTF-8, 192 bytes cada |
| 48, 832, 836 | Timestamp da confirmação, máscara dos indicadores desenhados, handle do consumidor |

As flags do produtor usam 1 para menu, 2 para sistema de combustível, 4 para veículo compatível, 8/16/32 para cada necessidade habilitada e 64 para modo de busca. A máscara do consumidor usa 4/8/16/32 para combustível/fome/sede/sono.
