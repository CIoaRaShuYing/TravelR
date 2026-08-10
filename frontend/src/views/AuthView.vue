<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { useRoute, useRouter } from 'vue-router'
import { api, type RegistrationMode } from '../api'
import { createSession } from '../session'

const router = useRouter()
const route = useRoute()
const loading = ref(false)
const authMode = ref<'login' | 'register'>('login')
const registrationMode = ref<RegistrationMode>('ApprovalRequired')
const initialAdministratorRegistration = ref(false)
const loginForm = reactive({ phoneNumber: '', password: '' })
const registerForm = reactive({ displayName: '', phoneNumber: '', password: '' })

const registrationLabel = computed(() => initialAdministratorRegistration.value
  ? '首位管理员待创建'
  : ({ Open: '开放注册', ApprovalRequired: '注册后等待管理员审批', Closed: '暂不开放注册' })[registrationMode.value])

async function loadSettings() {
  try {
    const settings = await api.getPublicSettings()
    registrationMode.value = settings.registrationMode
    initialAdministratorRegistration.value = settings.initialAdministratorRegistration
  } catch {
    ElMessage.warning('无法连接服务端，请确认 API 已启动。')
  }
}

async function signIn() {
  if (!/^1[3-9]\d{9}$/.test(loginForm.phoneNumber) || !loginForm.password) {
    ElMessage.error('请输入有效手机号和密码。')
    return
  }
  loading.value = true
  try {
    const result = await createSession(loginForm.phoneNumber.trim(), loginForm.password)
    if (result.profileIncomplete) await router.replace('/account/profile')
    else if (route.meta.administrator && !result.roles.includes('Administrator')) await router.replace('/claims')
    else if (route.path === '/') await router.replace('/claims')
    ElMessage.success(`欢迎回来，${result.user.displayName}`)
  } catch (error) {
    ElMessage.error(api.message(error, '手机号或密码不正确。'))
  } finally {
    loading.value = false
  }
}

async function register() {
  if (!registerForm.displayName.trim() || !/^1[3-9]\d{9}$/.test(registerForm.phoneNumber) || registerForm.password.length < 8) {
    ElMessage.error('请填写姓名、11 位手机号和至少 8 位密码。')
    return
  }
  loading.value = true
  try {
    const phoneNumber = registerForm.phoneNumber.trim()
    const result = await api.register({ ...registerForm, displayName: registerForm.displayName.trim(), phoneNumber })
    registerForm.displayName = ''
    registerForm.phoneNumber = ''
    registerForm.password = ''
    if (result.registrationCompleted) {
      loginForm.phoneNumber = phoneNumber
      loginForm.password = ''
      authMode.value = 'login'
      initialAdministratorRegistration.value = false
      ElMessage.success(result.message)
      await loadSettings()
    } else {
      authMode.value = 'login'
      ElMessage.info(result.message)
    }
  } catch (error) {
    ElMessage.error(api.message(error, '注册失败。'))
  } finally {
    loading.value = false
  }
}

onMounted(loadSettings)
</script>

<template>
  <main class="auth-layout">
    <aside class="auth-intro">
      <div class="brand brand--light"><span class="brand-mark">行</span><span>差旅账</span></div>
      <div class="intro-copy">
        <p class="eyebrow eyebrow--amber">PROJECT EXPENSE LEDGER</p>
        <h1>项目清楚，<br>每笔报销都有来路。</h1>
        <p>从行程凭证到审批发放，所有修改保留版本，所有状态可以追溯。</p>
      </div>
      <div class="ledger-rail" aria-label="报销流程">
        <span>项目</span><i></i><span>申请人</span><i></i><span>报销</span><i></i><span>发放</span>
      </div>
    </aside>

    <section class="auth-panel">
      <div class="auth-form">
        <p class="policy" :class="{ 'policy--initial': initialAdministratorRegistration }">
          <span class="policy-dot"></span>{{ registrationLabel }}
        </p>
        <header class="auth-heading">
          <h2>{{ authMode === 'login' ? '登录账户' : initialAdministratorRegistration ? '创建首位管理员' : '申请注册' }}</h2>
          <p>{{ authMode === 'login' ? '进入你的报销工作台。' : initialAdministratorRegistration ? '完成系统初始化后即可创建项目。' : registrationMode === 'ApprovalRequired' ? '提交后由管理员审批。' : '创建账户后即可使用。' }}</p>
        </header>

        <el-form v-if="authMode === 'login'" label-position="top" @submit.prevent="signIn">
          <el-form-item label="手机号"><el-input v-model="loginForm.phoneNumber" inputmode="numeric" maxlength="11" autocomplete="username" placeholder="11 位手机号" /></el-form-item>
          <el-form-item label="密码"><el-input v-model="loginForm.password" type="password" show-password autocomplete="current-password" placeholder="请输入密码" /></el-form-item>
          <el-button class="primary-action" native-type="submit" :loading="loading">登录</el-button>
        </el-form>

        <el-form v-else label-position="top" @submit.prevent="register">
          <div v-if="initialAdministratorRegistration" class="initial-admin-banner">
            <strong>此账户将成为首位管理员</strong>
            <span>可管理注册、项目、审批和发放，同时也可提交报销。</span>
          </div>
          <el-form-item label="姓名"><el-input v-model="registerForm.displayName" maxlength="100" placeholder="申请人姓名" /></el-form-item>
          <el-form-item label="手机号"><el-input v-model="registerForm.phoneNumber" inputmode="numeric" maxlength="11" autocomplete="username" placeholder="11 位手机号" /></el-form-item>
          <el-form-item label="密码"><el-input v-model="registerForm.password" type="password" show-password autocomplete="new-password" placeholder="至少 8 位" /></el-form-item>
          <el-button v-if="initialAdministratorRegistration || registrationMode !== 'Closed'" class="primary-action" native-type="submit" :loading="loading">{{ initialAdministratorRegistration ? '创建管理员' : '提交注册' }}</el-button>
          <el-alert v-else title="当前不开放注册，请联系管理员。" type="info" :closable="false" />
        </el-form>

        <button class="text-action" type="button" @click="authMode = authMode === 'login' ? 'register' : 'login'">
          {{ authMode === 'login' ? initialAdministratorRegistration ? '创建首位管理员' : '申请注册' : '返回登录' }}
        </button>
      </div>
    </section>
  </main>
</template>
