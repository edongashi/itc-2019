const path = require('path')
const fs = require('fs')
const util = require('util')
const { execFile } = require('child_process')
const glob = require('glob')
const { assign, sortBy } = require('lodash')
const { prompt } = require('enquirer')
const ora = require('ora')

const execFileAsync = util.promisify(execFile)

const exePaths = {
  win32: path.join(__dirname, '..', 'bin', 'win-x64', 'Timetabling.CLI.exe'),
  darwin: path.join(__dirname, '..', 'bin', 'linux-x64', 'Timetabling.CLI'),
  linux: path.join(__dirname, '..', 'bin', 'osx-x64', 'Timetabling.CLI')
}

const exePath = exePaths[process.platform]
if (!exePath) {
  console.error('Current operating system not supported.')
  process.exit(1)
}

if (!fs.existsSync(exePath)) {
  console.error('Executable not found. Please build the main project.')
  process.exit(1)
}

function mapValues(names) {
  return names.map((name) => this.find(name).value)
}

function pad(x) {
  return x.toString().padStart(2, '0')
}

function convertHMS(ms) {
  const sec = Math.round(ms / 1000)
  let hours = Math.floor(sec / 3600)
  let minutes = Math.floor((sec - hours * 3600) / 60)
  let seconds = sec - hours * 3600 - minutes * 60
  return pad(hours) + ':' + pad(minutes) + ':' + pad(seconds)
}

const f = (a, b) => [].concat(...a.map((d) => b.map((e) => [].concat(d, e))))
const cartesian = (a, b, ...c) => (b ? cartesian(f(a, b), ...c) : a)

async function runSteps(steps) {
  const result = {}
  for (let i = 0; i < steps.length; i++) {
    try {
      const step = steps[i]
      const promptResult = await prompt(
        typeof step === 'function' ? step(result) : step
      )
      assign(result, promptResult)
    } catch (e) {
      i = Math.max(-1, i - 2)
    }
  }

  return result
}

function runSolver(cwd, instance, config, runtime) {
  const args = [
    '--instance',
    instance,
    '--name',
    `${path.parse(instance).name}_${
      config ? path.parse(config).name : 'default'
    }`,
    '--timeout',
    Math.round(runtime * 60).toString(),
    '--quiet'
  ]

  if (config) {
    args.push('--config', config)
  }

  return execFileAsync(exePath, args, { cwd, windowsHide: true })
}

async function main() {
  const phases = { early: 1, middle: 2, late: 3, demo: 4 }

  const instancePaths = glob
    .sync(path.join(__dirname, 'instances', '**', '*.xml'))
    .map((filePath) => {
      const parts = filePath.split(/[\\\/]/g)
      return {
        path: filePath,
        phase: parts[parts.length - 2],
        name: parts[parts.length - 1]
      }
    })

  const instances = sortBy(instancePaths, [
    (x) => phases[x.phase],
    (x) => x.name
  ]).map((x) => ({
    name: `${x.phase}/${x.name}`,
    value: x.path
  }))

  const configFiles = glob
    .sync(path.join(__dirname, 'config', '*.cfg'))
    .map((filePath) => {
      const parts = filePath.split(/[\\\/]/g)
      return {
        name: parts[parts.length - 1],
        value: filePath
      }
    })

  const inputs = await runSteps([
    {
      name: 'instances',
      type: 'autocomplete',
      multiple: true,
      message: 'Select instances to run',
      limit: 10,
      choices: instances,
      result: mapValues,
      validate: (names) =>
        names.length === 0 ? 'At least one instance is required' : true
    },
    {
      name: 'configs',
      type: 'autocomplete',
      message: 'Select configurations to run each instance with',
      multiple: true,
      limit: 8,
      choices: sortBy(configFiles, (x) => x.name),
      result: mapValues
    },
    {
      name: 'runs',
      type: 'select',
      message: 'Select number of runs per instance/configuration combination',
      choices: [1, 2, 3, 4, 5, 6, 7, 8].map((x) => x.toString()),
      result: (x) => parseInt(x)
    },
    {
      name: 'runtime',
      type: 'numeral',
      message: 'Select solver runtime in minutes'
    },
    {
      name: 'directory',
      type: 'input',
      message: 'Provide a directory name to store results',
      validate: (x) => (x.length > 0 ? true : 'Provide a directory name')
    },
    (inputs) => {
      const solvers =
        inputs.instances.length *
        Math.max(1, inputs.configs.length) *
        inputs.runs
      return {
        name: 'confirmation',
        type: 'confirm',
        initial: true,
        message: `Start ${solvers} solver${
          solvers > 1 ? 's in parallel' : ''
        } for ${inputs.runtime} minutes${solvers > 1 ? ' each' : ''}?`
      }
    }
  ])

  if (!inputs.confirmation) {
    return
  }

  const cwd = path.join(__dirname, 'results', inputs.directory)

  fs.mkdirSync(cwd, { recursive: true })
  console.log(`Results will be stored in ${cwd}`)

  const started = Date.now()
  const spinnerMessage = () =>
    `Running solvers (${convertHMS(Date.now() - started)})`

  const spinner = ora(spinnerMessage()).start()

  const intervalHandle = setInterval(() => {
    spinner.text = spinnerMessage()
  }, 1000)

  const inputConfigs = [...inputs.configs]
  if (inputConfigs.length === 0) {
    inputConfigs.push(null)
  }

  const inputRuns = Array(inputs.runs).fill(null)
  const combinations = cartesian(inputs.instances, inputConfigs, inputRuns)
  const runtime = inputs.runtime

  try {
    await Promise.all(
      combinations.map(([instance, config]) => {
        return runSolver(cwd, instance, config, runtime)
      })
    )

    spinner.succeed()
  } catch (e) {
    spinner.fail()
    console.error(e)
  } finally {
    clearInterval(intervalHandle)
  }
}

main()
