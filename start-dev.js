const { spawn } = require('child_process');
const path = require('path');
const fs = require('fs');

console.log('🚀 Iniciando Locadora de Veículos...');

// Caminhos absolutos (ajuste conforme sua estrutura)
const basePath = __dirname;
const angularPath = path.join(basePath, 'Client');
const pythonPath = path.join(basePath, 'MachineLeaning');

console.log('📁 Caminhos:');
console.log(`   Angular: ${angularPath}`);
console.log(`   Python: ${pythonPath}`);

// Verificar se as pastas existem
if (!fs.existsSync(angularPath)) {
  console.error(`❌ Pasta do Angular não encontrada: ${angularPath}`);
  process.exit(1);
}

if (!fs.existsSync(pythonPath)) {
  console.error(`❌ Pasta do Python não encontrada: ${pythonPath}`);
  process.exit(1);
}

// Configurar processos
const processes = [];

// Função para iniciar um processo
function startProcess(name, command, args, cwd, color) {
  console.log(`\n▶️  Iniciando ${name}...`);
  
  const proc = spawn(command, args, {
    cwd,
    stdio: 'pipe',
    shell: true
  });

  // Colorir output baseado no serviço
  const colors = {
    angular: '\x1b[36m', // Cyan
    python: '\x1b[33m',  // Yellow
    reset: '\x1b[0m'
  };

  proc.stdout.on('data', (data) => {
    const output = data.toString();
    output.split('\n').forEach(line => {
      if (line.trim()) {
        console.log(`${colors[color]}${name}:${colors.reset} ${line}`);
      }
    });
  });

  proc.stderr.on('data', (data) => {
    const output = data.toString();
    output.split('\n').forEach(line => {
      if (line.trim()) {
        console.log(`${colors[color]}${name} ERROR:${colors.reset} ${line}`);
      }
    });
  });

  proc.on('close', (code) => {
    console.log(`${colors[color]}${name} encerrado com código ${code}${colors.reset}`);
  });

  processes.push({ name, process: proc });
  return proc;
}

// Iniciar Angular
const angular = startProcess(
  'ANGULAR',
  process.platform === 'win32' ? 'npm.cmd' : 'npm',
  ['start'],
  angularPath,
  'angular'
);

// Aguardar Angular iniciar
setTimeout(() => {
  // Iniciar Python
  const python = startProcess(
    'PYTHON',
    process.platform === 'win32' ? 'python' : 'python3',
    ['main.py'],
    pythonPath,
    'python'
  );
  
  console.log('\n✅ Serviços iniciados!');
  console.log('🌐 Angular: http://localhost:4200');
  console.log('🐍 Python API: http://localhost:8000');
  console.log('📚 Docs da API de ML: http://localhost:8000/docs');
  console.log('\n🔄 Use Ctrl+C para encerrar todos os serviços\n');
}, 5000);

// Manipular encerramento
process.on('SIGINT', () => {
  console.log('\n🛑 Encerrando serviços...');
  
  processes.forEach(({ name, process: proc }) => {
    console.log(`   Parando ${name}...`);
    proc.kill();
  });
  
  setTimeout(() => {
    console.log('👋 Até logo!');
    process.exit(0);
  }, 1000);
});